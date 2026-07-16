using ReportingService.Application.DTOs;

namespace ReportingService.Application.Interfaces;

public interface IIamServiceClient
{
    /// <summary>
    /// IamService's own audit log (login, user CRUD, password reset, lock/unlock), filtered and
    /// paginated. Returns null if IamService is unreachable — callers should degrade gracefully
    /// (skip this source) rather than fail the whole aggregated view.
    /// </summary>
    Task<IamAuditLogPageClientDto?> GetAuditLogsAsync(
        Guid? userId, string? entityType, string? action, int page, int pageSize, CancellationToken ct = default);
}
