using ReportingService.Application.DTOs;

namespace ReportingService.Application.Interfaces;

public interface IGlobalAuditLogService
{
    /// <summary>
    /// Aggregates audit log entries from IamService, GradingService and SubmissionService into
    /// one filtered, paginated, newest-first view. A source that's unreachable is skipped (listed
    /// in UnavailableSources) rather than failing the whole request — partial results beat none
    /// for an audit viewer.
    /// </summary>
    Task<GlobalAuditLogResultDto> GetAuditLogsAsync(
        Guid? userId, string? entityType, string? action, int page, int pageSize, CancellationToken ct = default);
}
