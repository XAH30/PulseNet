namespace PulseNet.Api.Domain;

/// <summary>
/// Нормализация и валидация поля «Пол» (gender).
/// Поддерживает совместимость с openapi.json (при отсутствии поля возвращает "unspecified").
/// </summary>
public static class Gender
{
    public const string Male = "male";
    public const string Female = "female";
    public const string Unspecified = "unspecified";

    public static bool TryNormalize(string? raw, out string normalized)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            normalized = Unspecified;
            return true;
        }

        if (raw.Contains('\0') || raw.Length > 32)
        {
            normalized = string.Empty;
            return false;
        }

        var token = raw.Trim().ToLowerInvariant();
        switch (token)
        {
            case "male" or "m" or "мужской" or "муж":
                normalized = Male;
                return true;
            case "female" or "f" or "женский" or "жен":
                normalized = Female;
                return true;
            case "unspecified" or "other" or "none" or "не указан":
                normalized = Unspecified;
                return true;
            default:
                normalized = string.Empty;
                return false;
        }
    }
}
