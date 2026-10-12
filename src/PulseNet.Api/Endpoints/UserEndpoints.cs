using PulseNet.Api.Contracts;
using PulseNet.Api.Data;
using PulseNet.Api.Domain;
using PulseNet.Api.Security;
using PulseNet.Api.Validation;

namespace PulseNet.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapPulseNetEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/user/register", HandleRegisterAsync);
        app.MapPost("/login", HandleLoginAsync);
        app.MapGet("/user/get/{id}", HandleGetUserByIdAsync);
        app.MapGet("/user/me", HandleGetCurrentUserAsync);
        app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "PulseNet" }));

        return app;
    }

    private static async Task<IResult> HandleRegisterAsync(
        HttpContext httpContext,
        RegisterUserRequest? request,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAuditLogger auditLogger,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        var todayUtc = DateOnly.FromDateTime(nowUtc.UtcDateTime);

        if (!InputValidator.TryValidateRegister(request, todayUtc, out var validated, out var error) || validated is null)
        {
            auditLogger.Record(new AuditEvent(
                Action: "user.register",
                Outcome: "invalid_input",
                SubjectUserId: null,
                RequestId: httpContext.TraceIdentifier,
                TimestampUtc: nowUtc));

            return Results.BadRequest(new { message = error ?? "Невалидные данные" });
        }

        var newUserId = UserId.New();
        var passwordHash = passwordHasher.HashPassword(validated.Password);
        var profile = new UserProfile(
            Id: newUserId,
            FirstName: validated.FirstName,
            SecondName: validated.SecondName,
            Birthdate: validated.Birthdate,
            Gender: validated.Gender,
            Biography: validated.Biography,
            City: validated.City,
            CreatedAt: nowUtc);

        await userRepository.CreateAsync(profile, passwordHash, cancellationToken);

        auditLogger.Record(new AuditEvent(
            Action: "user.register",
            Outcome: "success",
            SubjectUserId: newUserId,
            RequestId: httpContext.TraceIdentifier,
            TimestampUtc: nowUtc));

        return Results.Ok(new RegisterUserResponse(newUserId.ToString()));
    }

    private static async Task<IResult> HandleLoginAsync(
        HttpContext httpContext,
        LoginRequest? request,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IAuditLogger auditLogger,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();

        if (!InputValidator.TryValidateLogin(request, out var validated, out var error) || validated is null)
        {
            auditLogger.Record(new AuditEvent(
                Action: "user.login",
                Outcome: "invalid_input",
                SubjectUserId: null,
                RequestId: httpContext.TraceIdentifier,
                TimestampUtc: nowUtc));

            return Results.BadRequest(new { message = error ?? "Невалидные данные" });
        }

        var credentials = await userRepository.GetCredentialsByIdAsync(validated.UserId, cancellationToken);
        if (credentials is null)
        {
            // Защита от timing side-channel при проверке существования пользователя
            passwordHasher.VerifyDummy(validated.Password);

            auditLogger.Record(new AuditEvent(
                Action: "user.login",
                Outcome: "user_not_found",
                SubjectUserId: validated.UserId,
                RequestId: httpContext.TraceIdentifier,
                TimestampUtc: nowUtc));

            return Results.NotFound(new { message = "Пользователь не найден" });
        }

        if (!passwordHasher.VerifyPassword(validated.Password, credentials.PasswordHash))
        {
            auditLogger.Record(new AuditEvent(
                Action: "user.login",
                Outcome: "invalid_credentials",
                SubjectUserId: validated.UserId,
                RequestId: httpContext.TraceIdentifier,
                TimestampUtc: nowUtc));

            return Results.BadRequest(new { message = "Невалидные учетные данные" });
        }

        var token = tokenService.IssueToken(validated.UserId);

        auditLogger.Record(new AuditEvent(
            Action: "user.login",
            Outcome: "success",
            SubjectUserId: validated.UserId,
            RequestId: httpContext.TraceIdentifier,
            TimestampUtc: nowUtc));

        return Results.Ok(new LoginResponse(token));
    }

    private static async Task<IResult> HandleGetUserByIdAsync(
        string? id,
        IUserRepository userRepository,
        CancellationToken cancellationToken)
    {
        if (!UserId.TryParse(id, out var userId))
        {
            return Results.BadRequest(new { message = "Невалидный идентификатор пользователя" });
        }

        var profile = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (profile is null)
        {
            return Results.NotFound(new { message = "Анкета не найдена" });
        }

        return Results.Ok(UserDto.FromDomain(profile));
    }

    private static async Task<IResult> HandleGetCurrentUserAsync(
        HttpContext httpContext,
        IUserRepository userRepository,
        ITokenService tokenService,
        IAuditLogger auditLogger,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow();
        if (!TryExtractBearerToken(httpContext.Request.Headers.Authorization, out var rawToken) ||
            !tokenService.TryValidateToken(rawToken, out var principal) ||
            principal is null)
        {
            auditLogger.Record(new AuditEvent(
                Action: "user.profile.me",
                Outcome: "unauthorized",
                SubjectUserId: null,
                RequestId: httpContext.TraceIdentifier,
                TimestampUtc: nowUtc));

            return Results.Unauthorized();
        }

        // Проверка доступа по праву (scope), а не по имени роли
        if (!principal.Scopes.Contains(HmacTokenService.ScopeProfileReadSelf))
        {
            auditLogger.Record(new AuditEvent(
                Action: "user.profile.me",
                Outcome: "forbidden_missing_scope",
                SubjectUserId: principal.UserId,
                RequestId: httpContext.TraceIdentifier,
                TimestampUtc: nowUtc));

            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var profile = await userRepository.GetByIdAsync(principal.UserId, cancellationToken);
        if (profile is null)
        {
            return Results.NotFound(new { message = "Анкета не найдена" });
        }

        return Results.Ok(UserDto.FromDomain(profile));
    }

    private static bool TryExtractBearerToken(string? authorizationHeader, out string? token)
    {
        token = null;
        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return false;
        }

        const string prefix = "Bearer ";
        var trimmed = authorizationHeader.Trim();
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var extracted = trimmed[prefix.Length..].Trim();
        if (string.IsNullOrEmpty(extracted))
        {
            return false;
        }

        token = extracted;
        return true;
    }
}
