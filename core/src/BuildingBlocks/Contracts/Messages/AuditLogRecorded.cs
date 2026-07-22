namespace Contracts.Messages;

/// <summary>
/// Published (via a transactional outbox) by any service that records an audit-trail entry
/// (currently GradingService and IamService). ReportingService consumes these into its local
/// <c>audit_record</c> projection so the admin global audit-log viewer can be served from the
/// Reporting database instead of fanning out synchronous REST calls to each service. Append-only;
/// idempotent by <see cref="MessageId"/> (MassTransit inbox).
/// </summary>
public record AuditLogRecorded(
    Guid MessageId,
    string SourceService,
    Guid? UserId,
    string Action,
    string? EntityType,
    Guid? EntityId,
    string? OldValue,
    string? NewValue,
    string? Reason,
    DateTime OccurredAt
);
