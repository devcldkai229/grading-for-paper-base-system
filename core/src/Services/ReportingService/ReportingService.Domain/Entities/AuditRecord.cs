namespace ReportingService.Domain.Entities;

/// <summary>
/// Append-only audit-trail projection built from <c>AuditLogRecorded</c> events published by Grading
/// and Iam. Backs the admin global audit-log viewer (merged at query time with Submission's audit log,
/// which is still fetched over REST). Deduplicated on <see cref="MessageId"/>.
/// </summary>
public class AuditRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MessageId { get; set; }
    public string SourceService { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAt { get; set; }
}
