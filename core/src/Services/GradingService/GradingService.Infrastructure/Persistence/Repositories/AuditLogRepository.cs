using GradingService.Application.Interfaces;
using GradingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GradingService.Infrastructure.Persistence.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly GradingDbContext _db;

    public AuditLogRepository(GradingDbContext db)
    {
        _db = db;
    }

    public void Add(AuditLog log) => _db.AuditLogs.Add(log);

    public async Task<IReadOnlyList<AuditLog>> ListForEntityAsync(
        string entityType, Guid entityId, CancellationToken ct = default) =>
        await _db.AuditLogs
            .AsNoTracking()
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> ListAsync(
        Guid? userId, string? entityType, string? action, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (userId.HasValue) query = query.Where(l => l.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(l => l.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(l => l.Action == action);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
