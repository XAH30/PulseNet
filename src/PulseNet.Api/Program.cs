using Npgsql;
using PulseNet.Api.Contracts;
using PulseNet.Api.Data;
using PulseNet.Api.Endpoints;
using PulseNet.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 64 * 1024; // 64 KB защита от DoS большими телами
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IAuditLogger, StructuredAuditLogger>();

var tokenSecret = Environment.GetEnvironmentVariable("PULSENET_TOKEN_SECRET")
    ?? builder.Configuration["PulseNet:TokenSecret"]
    ?? "FAKEPII_DefaultDevSecretKey_777000001_PulseNet_32Bytes!";

builder.Services.AddSingleton<ITokenService>(sp =>
    new HmacTokenService(tokenSecret, sp.GetRequiredService<TimeProvider>()));

var dbConnectionString = Environment.GetEnvironmentVariable("PULSENET_DB_CONNECTION")
    ?? builder.Configuration.GetConnectionString("PulseNetDb")
    ?? "Host=localhost;Port=5432;Database=pulsenet;Username=postgres";

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(dbConnectionString));
builder.Services.AddSingleton<IUserRepository, PostgresUserRepository>();

var app = builder.Build();

// Глобальная обработка неперехваченных ошибок по контракту openapi.json (components/responses/5xx)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";

    try
    {
        await next();
    }
    catch (BadHttpRequestException)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Невалидный формат JSON-запроса." });
        }
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalExceptionHandler");
        logger.LogError(ex, "Unhandled server error for request_id={RequestId}", context.TraceIdentifier);

        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.Headers["Retry-After"] = "5";
            await context.Response.WriteAsJsonAsync(new ErrorResponse(
                Message: "Внутренняя ошибка сервера",
                RequestId: context.TraceIdentifier,
                Code: 500));
        }
    }
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPulseNetEndpoints();

// Поддержка прямых ссылок на страницу анкеты /profile/{id} во встроенном веб-интерфейсе
app.MapGet("/profile/{id}", async context =>
{
    var indexPath = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");
    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(indexPath);
});

var autoMigrate = builder.Configuration.GetValue("PulseNet:AutoEnsureSchema", true);
if (autoMigrate)
{
    using var scope = app.Services.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    await repo.EnsureSchemaAsync();
}

app.Run();

public partial class Program;
