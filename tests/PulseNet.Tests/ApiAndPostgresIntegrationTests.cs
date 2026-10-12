using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using PulseNet.Api.Contracts;
using PulseNet.Api.Data;
using PulseNet.Api.Domain;
using PulseNet.Api.Security;
using Xunit;

namespace PulseNet.Tests;

/// <summary>
/// Интеграционные тесты HTTP API, матрицы доступа (маршрут × право) и реального PostgreSQL 17
/// с проверкой устойчивости к SQL-инъекциям и безопасного хранения паролей.
/// Все тестовые данные строго синтетические (FAKEPII_, FAKE_, Zz_, 777000001+).
/// </summary>
public sealed class ApiAndPostgresIntegrationTests : IAsyncLifetime
{
    private readonly string _testDbName = $"pulsenet_test_{Guid.NewGuid():N}";
    private readonly string _adminConnString = "Host=localhost;Port=5432;Database=postgres;Username=xah30";
    private string _testConnString = string.Empty;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await using (var adminConn = new NpgsqlConnection(_adminConnString))
        {
            await adminConn.OpenAsync();
            await using var createCmd = new NpgsqlCommand($"CREATE DATABASE \"{_testDbName}\";", adminConn);
            await createCmd.ExecuteNonQueryAsync();
        }

        _testConnString = $"Host=localhost;Port=5432;Database={_testDbName};Username=xah30";

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<NpgsqlDataSource>();
                    services.RemoveAll<IUserRepository>();
                    services.AddSingleton(_ => NpgsqlDataSource.Create(_testConnString));
                    services.AddSingleton<IUserRepository, PostgresUserRepository>();
                });
            });

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        NpgsqlConnection.ClearAllPools();

        await using var adminConn = new NpgsqlConnection(_adminConnString);
        await adminConn.OpenAsync();
        await using var dropCmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_testDbName}\" WITH (FORCE);", adminConn);
        await dropCmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task FullLifecycle_Register_Login_GetById_And_VerifyPasswordHashAndNoSecondaryIndexesInPostgres()
    {
        var registerReq = new RegisterUserRequest(
            FirstName: "FAKE_Elena",
            SecondName: "Zz_Volkova",
            Birthdate: "1998-07-21",
            Biography: "FAKEPII_Distributed systems, PostgreSQL, Highload architecture",
            City: "FAKEPII_City_777000001",
            Password: "FAKEPII_SecretPass_900000001!",
            Gender: "female");

        var client = _client ?? throw new InvalidOperationException("HttpClient is not initialized.");
        var regResp = await client.PostAsJsonAsync("/user/register", registerReq);
        Assert.Equal(HttpStatusCode.OK, regResp.StatusCode);

        var regBody = await regResp.Content.ReadFromJsonAsync<RegisterUserResponse>();
        Assert.NotNull(regBody);
        Assert.True(Guid.TryParseExact(regBody!.UserId, "D", out var userGuid));

        // Проверка в PostgreSQL: пароль хранится только в виде PBKDF2-хеша, вторичных индексов нет
        await using (var conn = new NpgsqlConnection(_testConnString))
        {
            await conn.OpenAsync();

            await using var hashCmd = new NpgsqlCommand("SELECT password_hash FROM users WHERE id = @id;", conn);
            hashCmd.Parameters.AddWithValue("id", userGuid);
            var storedHash = (string?)await hashCmd.ExecuteScalarAsync();

            Assert.NotNull(storedHash);
            Assert.StartsWith("pbkdf2_sha256$100000$", storedHash!, StringComparison.Ordinal);
            Assert.DoesNotContain(registerReq.Password!, storedHash!, StringComparison.Ordinal);

            // В таблице users должен быть только 1 индекс (PRIMARY KEY users_pkey) — требование ТЗ «без индексов»
            await using var idxCmd = new NpgsqlCommand(
                "SELECT count(*) FROM pg_indexes WHERE tablename = 'users';",
                conn);
            var indexCount = Convert.ToInt32(await idxCmd.ExecuteScalarAsync());
            Assert.Equal(1, indexCount);
        }

        // Получение анкеты по ID (/user/get/{id})
        var getResp = await client.GetAsync($"/user/get/{regBody.UserId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var profile = await getResp.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(profile);
        Assert.Equal(regBody.UserId, profile!.Id);
        Assert.Equal("FAKE_Elena", profile.FirstName);
        Assert.Equal("Zz_Volkova", profile.SecondName);
        Assert.Equal("1998-07-21", profile.Birthdate);
        Assert.Equal("female", profile.Gender);
        Assert.Equal("FAKEPII_Distributed systems, PostgreSQL, Highload architecture", profile.Biography);
        Assert.Equal("FAKEPII_City_777000001", profile.City);

        // Успешный вход (/login)
        var loginResp = await client.PostAsJsonAsync(
            "/login",
            new LoginRequest(regBody.UserId, "FAKEPII_SecretPass_900000001!"));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);

        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginBody);
        Assert.False(string.IsNullOrWhiteSpace(loginBody!.Token));

        // Неверный пароль -> 400 Bad Request
        var badPassResp = await client.PostAsJsonAsync(
            "/login",
            new LoginRequest(regBody.UserId, "FAKEPII_WrongPass_900000002!"));
        Assert.Equal(HttpStatusCode.BadRequest, badPassResp.StatusCode);

        // Несуществующий UUID -> 404 Not Found
        var notFoundLoginResp = await client.PostAsJsonAsync(
            "/login",
            new LoginRequest("77700001-0000-4000-8000-000000000999", "FAKEPII_SecretPass_900000001!"));
        Assert.Equal(HttpStatusCode.NotFound, notFoundLoginResp.StatusCode);
    }

    [Fact]
    public async Task SqlInjectionPayloads_AreNeutralizedByParameterizedQueries_AndTableRemainsIntact()
    {
        var sqliFirstName = "FAKE_Name'); DROP TABLE users; --";
        var sqliSecondName = "Zz_Last' OR '1'='1";
        var sqliBiography = "FAKEPII_Bio'; UPDATE users SET password_hash = 'pwned' WHERE '1'='1'; --";
        var sqliCity = "FAKEPII_City' UNION SELECT id::text, password_hash FROM users --";

        var client = _client ?? throw new InvalidOperationException("HttpClient is not initialized.");
        var regResp = await client.PostAsJsonAsync(
            "/user/register",
            new RegisterUserRequest(
                FirstName: sqliFirstName,
                SecondName: sqliSecondName,
                Birthdate: "1992-11-03",
                Biography: sqliBiography,
                City: sqliCity,
                Password: "FAKEPII_SqliPass_900000001!' OR '1'='1"));

        Assert.Equal(HttpStatusCode.OK, regResp.StatusCode);
        var created = await regResp.Content.ReadFromJsonAsync<RegisterUserResponse>();
        Assert.NotNull(created);

        // Анкета возвращается с буквальными строками без исполнения SQL-инъекции
        var getResp = await client.GetAsync($"/user/get/{created!.UserId}");
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var dto = await getResp.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(dto);
        Assert.Equal(sqliFirstName, dto!.FirstName);
        Assert.Equal(sqliSecondName, dto.SecondName);
        Assert.Equal(sqliBiography, dto.Biography);
        Assert.Equal(sqliCity, dto.City);

        // Попытка SQL-инъекции через параметр пути /user/get/{id} и в /login
        var sqliGetResp = await client.GetAsync("/user/get/'%20OR%20'1'='1");
        Assert.Equal(HttpStatusCode.BadRequest, sqliGetResp.StatusCode);

        var sqliLoginResp = await client.PostAsJsonAsync(
            "/login",
            new LoginRequest($"{created.UserId}' OR '1'='1", "anything"));
        Assert.Equal(HttpStatusCode.BadRequest, sqliLoginResp.StatusCode);
    }

    [Fact]
    public async Task AccessControlMatrix_RouteByScope_EnforcesPermissionCheck()
    {
        var client = _client ?? throw new InvalidOperationException("HttpClient is not initialized.");

        // Регистрируем синтетического пользователя
        var regResp = await client.PostAsJsonAsync(
            "/user/register",
            new RegisterUserRequest(
                FirstName: "FAKE_Dmitry",
                SecondName: "Zz_Orlov",
                Birthdate: "1990-05-10",
                Biography: "FAKEPII_AccessMatrix_Test",
                City: "FAKEPII_City_777000002",
                Password: "FAKEPII_Pass_900000005!"));
        var regBody = await regResp.Content.ReadFromJsonAsync<RegisterUserResponse>();
        var userId = new UserId(Guid.Parse(regBody!.UserId));

        var tokenService = _factory!.Services.GetRequiredService<ITokenService>();

        // 1. Анонимный запрос к защищённому маршруту /user/me -> 401 Unauthorized
        var anonResp = await client.GetAsync("/user/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonResp.StatusCode);

        // 2. Токен БЕЗ права profile:read:self (только посторонний scope) -> 403 Forbidden
        var restrictedToken = tokenService.IssueToken(userId, scopes: ["profile:read:any"]);
        using var restrictedReq = new HttpRequestMessage(HttpMethod.Get, "/user/me");
        restrictedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", restrictedToken);
        var forbiddenResp = await client.SendAsync(restrictedReq);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResp.StatusCode);

        // 3. Токен с правом profile:read:self -> 200 OK
        var fullToken = tokenService.IssueToken(userId, scopes: [HmacTokenService.ScopeProfileReadSelf]);
        using var authorizedReq = new HttpRequestMessage(HttpMethod.Get, "/user/me");
        authorizedReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fullToken);
        var okResp = await client.SendAsync(authorizedReq);
        Assert.Equal(HttpStatusCode.OK, okResp.StatusCode);

        // 4. Проверка раздачи HTML-страницы анкеты (/ и /profile/{id})
        var indexResp = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, indexResp.StatusCode);
        var profileHtmlResp = await client.GetAsync($"/profile/{regBody.UserId}");
        Assert.Equal(HttpStatusCode.OK, profileHtmlResp.StatusCode);
    }
}
