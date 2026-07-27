using ReportingService.Application.DTOs;

namespace ReportingService.Application.Interfaces;

public interface ISubmissionServiceClient
{
    /// <summary>
    /// SubmissionService's own audit log (batch/paper deletion), filtered and paginated. Returns
    /// null if SubmissionService is unreachable — callers should degrade gracefully (skip this
    /// source) rather than fail the whole aggregated view.
    /// </summary>
    Task<SubmissionAuditLogPageClientDto?> GetAuditLogsAsync(
        Guid? userId, string? entityType, string? action, int page, int pageSize, CancellationToken ct = default);
}
