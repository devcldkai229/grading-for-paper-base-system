namespace SubmissionService.Application.Interfaces;

public interface ISubmissionAuditLogRepository
{
    /// <summary>Records an audited action (e.g. a batch/paper deletion) for compliance and traceability.</summary>
    Task LogAsync(
        string action, string entityType, Guid entityId, Guid subjectId, Guid performedBy,
        string? details = null, CancellationToken ct = default);
}
