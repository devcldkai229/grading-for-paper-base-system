using GradingService.Application.Interfaces;
using GradingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GradingService.Infrastructure.Persistence.Repositories;

public class GradingResumePointerRepository : IGradingResumePointerRepository
{
    private readonly GradingDbContext _db;

    public GradingResumePointerRepository(GradingDbContext db)
    {
        _db = db;
    }

    public Task<GradingResumePointer?> GetAsync(
        Guid teacherId, Guid batchId, bool asNoTracking, CancellationToken ct = default)
    {
        var query = _db.GradingResumePointers
            .Where(p => p.TeacherId == teacherId && p.BatchId == batchId);

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(ct);
    }

    public void Add(GradingResumePointer pointer) => _db.GradingResumePointers.Add(pointer);
}
