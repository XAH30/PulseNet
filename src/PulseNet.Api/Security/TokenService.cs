using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PulseNet.Api.Domain;

namespace PulseNet.Api.Security;

public sealed record TokenPrincipal(
    UserId UserId,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlySet<string> Scopes);

public interface ITokenService
{
    string IssueToken(UserId userId, IEnumerable<string>? scopes = null);
    bool TryValidateToken(string? rawToken, out TokenPrincipal? principal);
}

/// <summary>
/// Подписанный HMAC-SHA256 токен авторизации без необходимости внешнего кэша или таблиц сессий.
/// Проверяется константно по времени через CryptographicOperations.FixedTimeEquals.
/// </summary>
public sealed class HmacTokenService : ITokenService
{
    public const string ScopeProfileReadSelf = "profile:read:self";
    public const string ScopeProfileReadAny = "profile:read:any";

    private const string TokenPrefix = "pnet";
    private readonly byte[] _secretKey;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _tokenLifetime;

    public HmacTokenService(
        string secret,
        TimeProvider? timeProvider = null,
        TimeSpan? tokenLifetime = null)
    {
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new ArgumentException("Секрет токена должен быть не короче 32 байт.", nameof(secret));
        }

        _secretKey = Encoding.UTF8.GetBytes(secret);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _tokenLifetime = tokenLifetime ?? TimeSpan.FromHours(24);
    }

    public string IssueToken(UserId userId, IEnumerable<string>? scopes = null)
    {
        var now = _timeProvider.GetUtcNow();
        var exp = now.Add(_tokenLifetime).ToUnixTimeSeconds();
        var scopeArray = (scopes ?? [ScopeProfileReadSelf, ScopeProfileReadAny])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var payload = new TokenPayload(
            Sub: userId.ToString(),
            Exp: exp,
            Jti: Guid.NewGuid().ToString("N"),
            Scopes: scopeArray);

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var payloadB64 = Base64Url.EncodeToString(payloadBytes);

        var signatureBytes = ComputeSignature(payloadB64);
        var signatureB64 = Base64Url.EncodeToString(signatureBytes);

        return $"{TokenPrefix}.{payloadB64}.{signatureB64}";
    }

    public bool TryValidateToken(string? rawToken, out TokenPrincipal? principal)
    {
        principal = null;
        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length > 1024 || rawToken.Contains('\0'))
        {
            return false;
        }

        var parts = rawToken.Trim().Split('.');
        if (parts.Length != 3 || !string.Equals(parts[0], TokenPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        byte[] providedSignature;
        byte[] payloadBytes;
        try
        {
            providedSignature = Base64Url.DecodeFromChars(parts[2]);
            payloadBytes = Base64Url.DecodeFromChars(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedSignature = ComputeSignature(parts[1]);
        if (!CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
        {
            return false;
        }

        TokenPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TokenPayload>(payloadBytes);
        }
        catch (JsonException)
        {
            return false;
        }

        if (payload is null || !UserId.TryParse(payload.Sub, out var userId))
        {
            return false;
        }

        var nowUnix = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        if (payload.Exp <= nowUnix)
        {
            return false;
        }

        var scopes = new HashSet<string>(payload.Scopes ?? [], StringComparer.Ordinal);
        principal = new TokenPrincipal(
            UserId: userId,
            ExpiresAtUtc: DateTimeOffset.FromUnixTimeSeconds(payload.Exp),
            Scopes: scopes);
        return true;
    }

    private byte[] ComputeSignature(string payloadB64)
    {
        var data = Encoding.ASCII.GetBytes($"{TokenPrefix}.{payloadB64}");
        return HMACSHA256.HashData(_secretKey, data);
    }

    private sealed record TokenPayload(
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("exp")] long Exp,
        [property: JsonPropertyName("jti")] string Jti,
        [property: JsonPropertyName("scopes")] string[] Scopes);
}
