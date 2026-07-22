using Microsoft.EntityFrameworkCore;
using ReportingService.Application.Interfaces;
using ReportingService.Domain.Entities;

namespace ReportingService.Infrastructure.Persistence.Repositories;

public class FeedbackRecordReadRepository : IFeedbackRecordReadRepository
{
    private readonly ReportingDbContext _db;

    public FeedbackRecordReadRepository(ReportingDbContext db) => _db = db;

    public async Task<IReadOnlyList<FeedbackRecord>> GetBySubjectAsync(Guid subjectId, CancellationToken ct = default) =>
        await _db.FeedbackRecords
            .AsNoTracking()
            .Where(f => f.SubjectId == subjectId)
            .OrderBy(f => f.AliasNumber ?? int.MaxValue)
            .ToListAsync(ct);
}
