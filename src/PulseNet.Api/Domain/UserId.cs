namespace PulseNet.Api.Domain;

/// <summary>
/// Строго типизированный идентификатор пользователя (UUID в каноническом формате D).
/// Закрывает класс инъекций и невалидных идентификаторов на границе входа.
/// </summary>
public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());

    public static bool TryParse(string? raw, out UserId userId)
    {
        userId = default;
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 64 || raw.Contains('\0'))
        {
            return false;
        }

        if (!Guid.TryParseExact(raw.Trim(), "D", out var parsed) || parsed == Guid.Empty)
        {
            return false;
        }

        userId = new UserId(parsed);
        return true;
    }

    public override string ToString() => Value.ToString("D");
}
