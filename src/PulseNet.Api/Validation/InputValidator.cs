using System.Globalization;
using PulseNet.Api.Contracts;
using PulseNet.Api.Domain;

namespace PulseNet.Api.Validation;

public sealed record ValidatedRegistration(
    string FirstName,
    string SecondName,
    DateOnly Birthdate,
    string Gender,
    string Biography,
    string City,
    string Password);

public sealed record ValidatedLogin(
    UserId UserId,
    string Password);

/// <summary>
/// Единая точка валидации входных данных на границе HTTP API (By-Construction).
/// Отсекает пустые поля, превышение длины, управляющие символы/NUL-байты и невалидные даты.
/// </summary>
public static class InputValidator
{
    public const int MaxNameLength = 100;
    public const int MaxCityLength = 100;
    public const int MaxBiographyLength = 2000;
    public const int MinPasswordLength = 6;
    public const int MaxPasswordLength = 128;
    private static readonly DateOnly MinBirthdate = new(1900, 1, 1);

    public static bool TryValidateRegister(
        RegisterUserRequest? request,
        DateOnly todayUtc,
        out ValidatedRegistration? validated,
        out string? error)
    {
        validated = null;
        if (request is null)
        {
            error = "Тело запроса не задано.";
            return false;
        }

        if (!TrySanitizeText(request.FirstName, MaxNameLength, allowMultiline: false, out var firstName))
        {
            error = "Поле first_name обязательно (1..100 символов без управляющих кодов).";
            return false;
        }

        if (!TrySanitizeText(request.SecondName, MaxNameLength, allowMultiline: false, out var secondName))
        {
            error = "Поле second_name обязательно (1..100 символов без управляющих кодов).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Birthdate) ||
            !DateOnly.TryParseExact(
                request.Birthdate.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var birthdate) ||
            birthdate < MinBirthdate ||
            birthdate > todayUtc)
        {
            error = "Поле birthdate должно быть валидной датой в формате YYYY-MM-DD (не ранее 1900-01-01 и не в будущем).";
            return false;
        }

        if (!Gender.TryNormalize(request.Gender, out var gender))
        {
            error = "Поле gender должно принимать одно из значений: male, female, unspecified.";
            return false;
        }

        if (!TrySanitizeText(request.Biography, MaxBiographyLength, allowMultiline: true, out var biography))
        {
            error = "Поле biography (интересы) обязательно (1..2000 символов без управляющих кодов).";
            return false;
        }

        if (!TrySanitizeText(request.City, MaxCityLength, allowMultiline: false, out var city))
        {
            error = "Поле city обязательно (1..100 символов без управляющих кодов).";
            return false;
        }

        if (!TryValidatePassword(request.Password, requireMinLength: true, out var password))
        {
            error = $"Поле password обязательно ({MinPasswordLength}..{MaxPasswordLength} символов без NUL-байтов).";
            return false;
        }

        validated = new ValidatedRegistration(
            FirstName: firstName,
            SecondName: secondName,
            Birthdate: birthdate,
            Gender: gender,
            Biography: biography,
            City: city,
            Password: password);
        error = null;
        return true;
    }

    public static bool TryValidateLogin(
        LoginRequest? request,
        out ValidatedLogin? validated,
        out string? error)
    {
        validated = null;
        if (request is null)
        {
            error = "Тело запроса не задано.";
            return false;
        }

        if (!UserId.TryParse(request.Id, out var userId))
        {
            error = "Поле id должно быть валидным UUID пользователя.";
            return false;
        }

        if (!TryValidatePassword(request.Password, requireMinLength: false, out var password))
        {
            error = "Поле password обязательно и не может превышать 128 символов.";
            return false;
        }

        validated = new ValidatedLogin(userId, password);
        error = null;
        return true;
    }

    private static bool TrySanitizeText(
        string? raw,
        int maxLength,
        bool allowMultiline,
        out string sanitized)
    {
        sanitized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
        {
            return false;
        }

        foreach (var ch in trimmed)
        {
            if (ch == '\0')
            {
                return false;
            }

            if (char.IsControl(ch))
            {
                if (allowMultiline && (ch == '\n' || ch == '\r' || ch == '\t'))
                {
                    continue;
                }

                return false;
            }
        }

        sanitized = trimmed;
        return true;
    }

    private static bool TryValidatePassword(
        string? raw,
        bool requireMinLength,
        out string password)
    {
        password = string.Empty;
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        var minLen = requireMinLength ? MinPasswordLength : 1;
        if (raw.Length < minLen || raw.Length > MaxPasswordLength || raw.Contains('\0'))
        {
            return false;
        }

        password = raw;
        return true;
    }
}
