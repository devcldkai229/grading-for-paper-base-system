using ReportingService.Domain.Entities;

namespace ReportingService.Application.Interfaces;

/// <summary>
/// Reads the local audit-trail projection (Grading + Iam entries, populated by the AuditLogRecorded
/// consumer). Returns the newest <paramref name="take"/> entries matching the optional filters plus the
/// total matching count (for accurate pagination alongside the still-REST Submission audit source).
/// </summary>
public interface IAuditRecordReadRepository
{
    Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> QueryAsync(
        Guid? userId, string? entityType, string? action, int take, CancellationToken ct = default);
}
