using System.Globalization;
using System.Security.Cryptography;

namespace PulseNet.Api.Security;

public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
    void VerifyDummy(string password);
}

/// <summary>
/// Безопасное хеширование паролей через PBKDF2-HMAC-SHA256 с криптостойкой 16-байтной солью
/// и константным по времени сравнением (CryptographicOperations.FixedTimeEquals).
/// Формат: pbkdf2_sha256$&lt;iterations&gt;$&lt;base64_salt&gt;$&lt;base64_subkey&gt;
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const string AlgorithmPrefix = "pbkdf2_sha256";
    public const int DefaultIterations = 100_000;
    public const int SaltSizeBytes = 16;
    public const int KeySizeBytes = 32;

    private static readonly byte[] DummySalt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
    private static readonly byte[] DummyKey = Rfc2898DeriveBytes.Pbkdf2(
        "FAKEPII_DummyPassword_900000001",
        DummySalt,
        DefaultIterations,
        HashAlgorithmName.SHA256,
        KeySizeBytes);

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        Span<byte> salt = stackalloc byte[SaltSizeBytes];
        Span<byte> subkey = stackalloc byte[KeySizeBytes];

        RandomNumberGenerator.Fill(salt);
        Rfc2898DeriveBytes.Pbkdf2(
            password.AsSpan(),
            salt,
            subkey,
            DefaultIterations,
            HashAlgorithmName.SHA256);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{AlgorithmPrefix}${DefaultIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(subkey)}");
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], AlgorithmPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) ||
            iterations < 10_000 ||
            iterations > 2_000_000)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedSubkey;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedSubkey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length != SaltSizeBytes || expectedSubkey.Length != KeySizeBytes)
        {
            return false;
        }

        Span<byte> actualSubkey = stackalloc byte[KeySizeBytes];
        Rfc2898DeriveBytes.Pbkdf2(
            password.AsSpan(),
            salt,
            actualSubkey,
            iterations,
            HashAlgorithmName.SHA256);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }

    public void VerifyDummy(string password)
    {
        Span<byte> actualSubkey = stackalloc byte[KeySizeBytes];
        Rfc2898DeriveBytes.Pbkdf2(
            (password ?? string.Empty).AsSpan(),
            DummySalt,
            actualSubkey,
            DefaultIterations,
            HashAlgorithmName.SHA256);
        _ = CryptographicOperations.FixedTimeEquals(actualSubkey, DummyKey);
    }
}
