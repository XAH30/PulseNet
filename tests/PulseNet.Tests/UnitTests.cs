using Microsoft.Extensions.Logging.Abstractions;
using PulseNet.Api.Contracts;
using PulseNet.Api.Domain;
using PulseNet.Api.Security;
using PulseNet.Api.Validation;
using Xunit;

namespace PulseNet.Tests;

public sealed class UnitTests
{
    private static readonly DateOnly FixedToday = new(2026, 10, 12);

    [Fact]
    public void PasswordHasher_HashesAndVerifies_WithUniqueRandomSalt()
    {
        var hasher = new Pbkdf2PasswordHasher();
        const string password = "FAKEPII_Pass_900000001!";

        var hash1 = hasher.HashPassword(password);
        var hash2 = hasher.HashPassword(password);

        Assert.StartsWith("pbkdf2_sha256$100000$", hash1, StringComparison.Ordinal);
        Assert.NotEqual(hash1, hash2);
        Assert.DoesNotContain(password, hash1, StringComparison.Ordinal);

        Assert.True(hasher.VerifyPassword(password, hash1));
        Assert.True(hasher.VerifyPassword(password, hash2));
        Assert.False(hasher.VerifyPassword("FAKEPII_WrongPass_900000002!", hash1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("plaintext_password")]
    [InlineData("bcrypt$100000$AAAA$BBBB")]
    [InlineData("pbkdf2_sha256$999$AAAA$BBBB")]
    [InlineData("pbkdf2_sha256$9999999$AAAA$BBBB")]
    [InlineData("pbkdf2_sha256$100000$not-base64!$$not-base64!$")]
    [InlineData("pbkdf2_sha256$100000$AQID$AQID")]
    public void PasswordHasher_RejectsMalformedOrWeakHashes_FailClosed(string? malformedHash)
    {
        var hasher = new Pbkdf2PasswordHasher();
        Assert.False(hasher.VerifyPassword("FAKEPII_Pass_900000001!", malformedHash ?? string.Empty));
    }

    [Fact]
    public void TokenService_IssuesAndValidates_AndRejectsTamperedOrExpiredTokens()
    {
        var clock = new AdjustableTimeProvider(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
        var service = new HmacTokenService(
            secret: "FAKEPII_TestSecretKey_777000001_32BytesMinimum!",
            timeProvider: clock,
            tokenLifetime: TimeSpan.FromMinutes(30));

        var userId = new UserId(Guid.Parse("77700001-0000-4000-8000-000000000001"));
        var token = service.IssueToken(userId);

        Assert.True(service.TryValidateToken(token, out var principal));
        Assert.NotNull(principal);
        Assert.Equal(userId, principal!.UserId);
        Assert.Contains(HmacTokenService.ScopeProfileReadSelf, principal.Scopes);

        // Подделка последнего символа подписи
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');
        Assert.False(service.TryValidateToken(tampered, out _));

        // Истечение срока жизни токена
        clock.Advance(TimeSpan.FromMinutes(31));
        Assert.False(service.TryValidateToken(token, out _));
    }

    [Fact]
    public void TokenService_RejectsShortSecret_ByConstruction()
    {
        Assert.Throws<ArgumentException>(() => new HmacTokenService("short_secret"));
    }

    [Fact]
    public void InputValidator_AcceptsValidOpenApiRequest_WithoutGender_AndNormalizesToUnspecified()
    {
        var request = new RegisterUserRequest(
            FirstName: "FAKE_Alexey",
            SecondName: "Zz_Smirnov",
            Birthdate: "1995-04-15",
            Biography: "FAKEPII_Highload systems, PostgreSQL, C# .NET 10",
            City: "FAKEPII_City_900000001",
            Password: "FAKEPII_Pass_900000001!",
            Gender: null);

        var ok = InputValidator.TryValidateRegister(request, FixedToday, out var validated, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.NotNull(validated);
        Assert.Equal(Gender.Unspecified, validated!.Gender);
        Assert.Equal(new DateOnly(1995, 4, 15), validated.Birthdate);
    }

    [Theory]
    [InlineData("male", "male")]
    [InlineData("FEMALE", "female")]
    [InlineData("Мужской", "male")]
    [InlineData("женский", "female")]
    [InlineData("Не указан", "unspecified")]
    [InlineData("", "unspecified")]
    public void InputValidator_NormalizesSupportedGenderValues(string rawGender, string expected)
    {
        Assert.True(Gender.TryNormalize(rawGender, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("", "Zz_Last", "1995-01-01", "male", "Bio", "City", "FAKEPII_Pass123")]
    [InlineData("FAKE_First", "", "1995-01-01", "male", "Bio", "City", "FAKEPII_Pass123")]
    [InlineData("FAKE_First", "Zz_Last", "2099-01-01", "male", "Bio", "City", "FAKEPII_Pass123")]
    [InlineData("FAKE_First", "Zz_Last", "1899-12-31", "male", "Bio", "City", "FAKEPII_Pass123")]
    [InlineData("FAKE_First", "Zz_Last", "15.04.1995", "male", "Bio", "City", "FAKEPII_Pass123")]
    [InlineData("FAKE_First", "Zz_Last", "1995-01-01", "invalid_gender", "Bio", "City", "FAKEPII_Pass123")]
    [InlineData("FAKE_First", "Zz_Last", "1995-01-01", "male", "", "City", "FAKEPII_Pass123")]
    [InlineData("FAKE_First", "Zz_Last", "1995-01-01", "male", "Bio", "", "FAKEPII_Pass123")]
    [InlineData("FAKE_First", "Zz_Last", "1995-01-01", "male", "Bio", "City", "short")]
    [InlineData("FAKE\0Null", "Zz_Last", "1995-01-01", "male", "Bio", "City", "FAKEPII_Pass123")]
    public void InputValidator_RejectsInvalidRegistrationInputs(
        string firstName,
        string secondName,
        string birthdate,
        string gender,
        string biography,
        string city,
        string password)
    {
        var req = new RegisterUserRequest(firstName, secondName, birthdate, biography, city, password, gender);
        Assert.False(InputValidator.TryValidateRegister(req, FixedToday, out _, out var err));
        Assert.False(string.IsNullOrWhiteSpace(err));
    }

    [Fact]
    public void AuditLogger_ThrowsWhenEventIsIncomplete_FailClosed()
    {
        var logger = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);

        Assert.Throws<InvalidOperationException>(() =>
            logger.Record(new AuditEvent(
                Action: "",
                Outcome: "success",
                SubjectUserId: null,
                RequestId: "req-900000001",
                TimestampUtc: DateTimeOffset.UtcNow)));
    }

    private sealed class AdjustableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public AdjustableTimeProvider(DateTimeOffset initialUtc) => _now = initialUtc;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
