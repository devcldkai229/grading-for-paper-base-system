using GradingService.Domain.Entities;

namespace GradingService.Application.Interfaces;

public interface IAuditLogRepository
{
    /// <summary>Stages a new audit log entry for insertion (caller must call IUnitOfWork.SaveChangesAsync).</summary>
    void Add(AuditLog log);

    /// <summary>Lists (untracked) audit entries for an entity, newest first.</summary>
    Task<IReadOnlyList<AuditLog>> ListForEntityAsync(
        string entityType, Guid entityId, CancellationToken ct = default);

    /// <summary>
    /// Lists (untracked) audit entries across all entities, newest first, optionally filtered by
    /// user/entityType/action. Consumed by ReportingService's global audit log viewer.
    /// </summary>
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> ListAsync(
        Guid? userId, string? entityType, string? action, int page, int pageSize, CancellationToken ct = default);
}
