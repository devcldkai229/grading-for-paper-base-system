using GradingService.Domain.Entities;

namespace GradingService.Application.Interfaces;

public interface IAuditLogRepository
{
    /// <summary>Stages a new audit log entry for insertion (caller must call IUnitOfWork.SaveChangesAsync).</summary>
    void Add(AuditLog log);

    /// <summary>Lists (untracked) audit entries for an entity, newest first.</summary>
    Task<IReadOnlyList<AuditLog>> ListForEntityAsync(
        string entityType, Guid entityId, CancellationToken ct = default);
}
