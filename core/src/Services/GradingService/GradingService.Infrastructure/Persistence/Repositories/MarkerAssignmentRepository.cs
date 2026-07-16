using GradingService.Application.Interfaces;
using GradingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GradingService.Infrastructure.Persistence.Repositories;

public class MarkerAssignmentRepository : IMarkerAssignmentRepository
{
    private readonly GradingDbContext _db;

    public MarkerAssignmentRepository(GradingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AliasRangeDto>> GetAliasRangesAsync(
        Guid subjectId, Guid lecturerId, CancellationToken ct = default) =>
        await _db.MarkerAssignments
            .AsNoTracking()
            .Where(m => m.SubjectId == subjectId && m.TeacherId == lecturerId)
            .OrderBy(m => m.AliasStart)
            .Select(m => new AliasRangeDto(m.AliasStart, m.AliasEnd))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetAssignedSubjectIdsAsync(
        Guid lecturerId, CancellationToken ct = default) =>
        await _db.MarkerAssignments
            .AsNoTracking()
            .Where(m => m.TeacherId == lecturerId)
            .Select(m => m.SubjectId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MarkerAssignment>> ListForSubjectAsync(
        Guid subjectId, CancellationToken ct = default) =>
        await _db.MarkerAssignments
            .AsNoTracking()
            .Where(m => m.SubjectId == subjectId)
            .OrderBy(m => m.AliasStart)
            .ToListAsync(ct);

    public Task<MarkerAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.MarkerAssignments.FirstOrDefaultAsync(m => m.Id == id, ct);

    public void Add(MarkerAssignment assignment) => _db.MarkerAssignments.Add(assignment);

    public void Remove(MarkerAssignment assignment) => _db.MarkerAssignments.Remove(assignment);
}
