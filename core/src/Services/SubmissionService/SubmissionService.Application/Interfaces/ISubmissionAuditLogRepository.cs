using SubmissionService.Domain.Entities;

namespace SubmissionService.Application.Interfaces;

public interface ISubmissionAuditLogRepository
{
    /// <summary>Records an audited action (e.g. a batch/paper deletion) for compliance and traceability.</summary>
    Task LogAsync(
        string action, string entityType, Guid entityId, Guid subjectId, Guid performedBy,
        string? details = null, CancellationToken ct = default);

    /// <summary>
    /// Lists audit entries, newest first, optionally filtered by user (performedBy)/entityType/action.
    /// Consumed by ReportingService's global audit log viewer.
    /// </summary>
    Task<(IReadOnlyList<SubmissionAuditLog> Items, int TotalCount)> ListAsync(
        Guid? userId, string? entityType, string? action, int page, int pageSize, CancellationToken ct = default);
}
