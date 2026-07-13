namespace SubmissionService.Domain.Entities;

public sealed class SubmissionAuditLog
{
    public Guid Id { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public Guid SubjectId { get; set; }

    public Guid PerformedBy { get; set; }

    public string? Details { get; set; }

    public DateTime PerformedAt { get; set; }
}
