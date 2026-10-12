using PulseNet.Api.Domain;

namespace PulseNet.Api.Security;

public sealed record AuditEvent(
    string Action,
    string Outcome,
    UserId? SubjectUserId,
    string RequestId,
    DateTimeOffset TimestampUtc);

public interface IAuditLogger
{
    void Record(AuditEvent auditEvent);
}

/// <summary>
/// Синхронный fail-closed аудит изменяющих и чувствительных действий.
/// Никогда не логирует пароли, токены или ПДн (только код события, исход, UUID и request_id).
/// </summary>
public sealed class StructuredAuditLogger : IAuditLogger
{
    private readonly ILogger<StructuredAuditLogger> _logger;

    public StructuredAuditLogger(ILogger<StructuredAuditLogger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Record(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        if (string.IsNullOrWhiteSpace(auditEvent.Action) ||
            string.IsNullOrWhiteSpace(auditEvent.Outcome) ||
            string.IsNullOrWhiteSpace(auditEvent.RequestId))
        {
            throw new InvalidOperationException("Событие аудита не прошло fail-closed проверку целостности.");
        }

        _logger.LogInformation(
            "AUDIT action={Action} outcome={Outcome} subject_user_id={SubjectUserId} request_id={RequestId} timestamp_utc={TimestampUtc:O}",
            auditEvent.Action,
            auditEvent.Outcome,
            auditEvent.SubjectUserId?.ToString() ?? "anonymous",
            auditEvent.RequestId,
            auditEvent.TimestampUtc);
    }
}
