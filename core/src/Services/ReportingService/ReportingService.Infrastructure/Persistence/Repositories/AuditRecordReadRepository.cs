using Microsoft.EntityFrameworkCore;
using ReportingService.Application.Interfaces;
using ReportingService.Domain.Entities;

namespace ReportingService.Infrastructure.Persistence.Repositories;

public class AuditRecordReadRepository : IAuditRecordReadRepository
{
    private readonly ReportingDbContext _db;

    public AuditRecordReadRepository(ReportingDbContext db) => _db = db;

    public async Task<(IReadOnlyList<AuditRecord> Items, int TotalCount)> QueryAsync(
        Guid? userId, string? entityType, string? action, int take, CancellationToken ct = default)
    {
        var query = _db.AuditRecords.AsNoTracking().AsQueryable();

        if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.OccurredAt)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
