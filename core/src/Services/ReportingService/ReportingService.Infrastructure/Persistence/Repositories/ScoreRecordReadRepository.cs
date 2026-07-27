using Microsoft.EntityFrameworkCore;
using ReportingService.Application.Interfaces;
using ReportingService.Domain.Entities;

namespace ReportingService.Infrastructure.Persistence.Repositories;

public class ScoreRecordReadRepository : IScoreRecordReadRepository
{
    private readonly ReportingDbContext _db;

    public ScoreRecordReadRepository(ReportingDbContext db) => _db = db;

    public async Task<IReadOnlyList<ScoreRecord>> GetBySubjectsAsync(
        IReadOnlyCollection<Guid> subjectIds, CancellationToken ct = default)
    {
        if (subjectIds.Count == 0) return Array.Empty<ScoreRecord>();
        return await _db.ScoreRecords
            .AsNoTracking()
            .Where(s => subjectIds.Contains(s.SubjectId))
            .ToListAsync(ct);
    }
}
