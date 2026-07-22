using Microsoft.EntityFrameworkCore;
using ReportingService.Application.Interfaces;
using ReportingService.Domain.Entities;

namespace ReportingService.Infrastructure.Persistence.Repositories;

public class ProgressReadRepository : IProgressReadRepository
{
    private readonly ReportingDbContext _db;

    public ProgressReadRepository(ReportingDbContext db) => _db = db;

    public async Task<IReadOnlyList<SubjectProgress>> GetSubjectProgressAsync(
        IReadOnlyCollection<Guid> subjectIds, CancellationToken ct = default)
    {
        if (subjectIds.Count == 0) return Array.Empty<SubjectProgress>();
        return await _db.SubjectProgress
            .AsNoTracking()
            .Where(s => subjectIds.Contains(s.SubjectId))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MarkerProgress>> GetMarkerProgressAsync(
        IReadOnlyCollection<Guid> subjectIds, CancellationToken ct = default)
    {
        if (subjectIds.Count == 0) return Array.Empty<MarkerProgress>();
        return await _db.MarkerProgress
            .AsNoTracking()
            .Where(m => subjectIds.Contains(m.SubjectId))
            .ToListAsync(ct);
    }
}
