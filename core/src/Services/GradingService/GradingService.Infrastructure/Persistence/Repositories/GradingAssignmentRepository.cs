using GradingService.Application.Interfaces;
using GradingService.Domain.Entities;
using GradingService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GradingService.Infrastructure.Persistence.Repositories;

public class GradingAssignmentRepository : IGradingAssignmentRepository
{
    private readonly GradingDbContext _db;

    public GradingAssignmentRepository(GradingDbContext db)
    {
        _db = db;
    }

    public Task<GradingAssignment?> GetWithFormAndDetailsAsync(
        Guid assignmentId, Guid? teacherId, bool asNoTracking, CancellationToken ct = default)
    {
        var query = _db.GradingAssignments
            .Include(a => a.GradingForm!)
                .ThenInclude(f => f.QuestionGradeDetails)
            .Where(a => a.Id == assignmentId);

        if (teacherId.HasValue)
        {
            query = query.Where(a => a.TeacherId == teacherId.Value);
        }

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(ct);
    }

    public Task<GradingAssignment?> GetWithFormAsync(
        Guid assignmentId, Guid? teacherId, bool asNoTracking, CancellationToken ct = default)
    {
        var query = _db.GradingAssignments
            .Include(a => a.GradingForm)
            .Where(a => a.Id == assignmentId);

        if (teacherId.HasValue)
        {
            query = query.Where(a => a.TeacherId == teacherId.Value);
        }

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(ct);
    }

    public Task<GradingAssignment?> FindFirstGradeForPaperAsync(
        Guid studentPaperId, Guid teacherId, CancellationToken ct = default) =>
        _db.GradingAssignments
            .Include(a => a.GradingForm!)
                .ThenInclude(f => f.QuestionGradeDetails)
            .FirstOrDefaultAsync(a =>
                a.StudentPaperId == studentPaperId
                && a.TeacherId == teacherId
                && a.AssignmentType == AssignmentType.FirstGrade, ct);

    public async Task<IReadOnlyList<GradingAssignment>> ListByTeacherAndPaperIdsAsync(
        Guid teacherId, IReadOnlyCollection<Guid> paperIds, CancellationToken ct = default) =>
        await _db.GradingAssignments
            .AsNoTracking()
            .Where(a => a.TeacherId == teacherId && paperIds.Contains(a.StudentPaperId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<GradingAssignment>> ListBySubjectWithFormsAsync(
        Guid subjectId, bool submittedOnly, CancellationToken ct = default)
    {
        var query = _db.GradingAssignments
            .AsNoTracking()
            .Include(a => a.GradingForm!)
                .ThenInclude(f => f.QuestionGradeDetails)
            .Where(a => a.SubjectId == subjectId);

        if (submittedOnly)
        {
            query = query.Where(a => a.Status == GradingProgressStatus.Submitted);
        }

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<GradingAssignment>> ListByTeacherWithFormsAsync(
        Guid teacherId, CancellationToken ct = default) =>
        await _db.GradingAssignments
            .AsNoTracking()
            .Include(a => a.GradingForm!)
                .ThenInclude(f => f.QuestionGradeDetails)
            .Where(a => a.TeacherId == teacherId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AssignmentProgressRow>> ListProgressByTeacherAsync(
        Guid teacherId, CancellationToken ct = default) =>
        await _db.GradingAssignments
            .AsNoTracking()
            .Where(a => a.TeacherId == teacherId)
            .Select(a => new AssignmentProgressRow(a.Id, a.SubjectId, a.Status, a.CreatedAt))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SubjectProgressRow>> ListProgressRowsAsync(
        IReadOnlyCollection<Guid>? subjectIds, CancellationToken ct = default)
    {
        var query = _db.GradingAssignments.AsNoTracking().AsQueryable();
        if (subjectIds is { Count: > 0 })
        {
            query = query.Where(a => subjectIds.Contains(a.SubjectId));
        }

        return await query
            .Select(a => new SubjectProgressRow(
                a.SubjectId,
                a.TeacherId,
                a.Status,
                a.GradingForm != null ? a.GradingForm.SubmittedAt : null,
                a.GradingForm != null ? (decimal?)a.GradingForm.TotalScore : null,
                a.GradingForm != null ? a.GradingForm.ActiveSecondsSpent : 0,
                a.IsFlagged))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SubjectScoreRow>> ListSubmittedScoresAsync(
        IReadOnlyCollection<Guid>? subjectIds, CancellationToken ct = default)
    {
        var query = _db.GradingAssignments.AsNoTracking()
            .Where(a => a.Status == GradingProgressStatus.Submitted && a.GradingForm != null);

        if (subjectIds is { Count: > 0 })
        {
            query = query.Where(a => subjectIds.Contains(a.SubjectId));
        }

        return await query
            .Select(a => new SubjectScoreRow(a.SubjectId, a.GradingForm!.TotalScore))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<GradingQueueRow>> ListQueueRowsAsync(
        Guid teacherId, GradingProgressStatus? status, bool? flaggedOnly, CancellationToken ct = default)
    {
        var query = _db.GradingAssignments.AsNoTracking().Where(a => a.TeacherId == teacherId);

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        if (flaggedOnly == true)
        {
            query = query.Where(a => a.IsFlagged);
        }

        return await query
            .Select(a => new GradingQueueRow(
                a.Id,
                a.StudentPaperId,
                a.SubjectId,
                a.Status,
                a.IsFlagged,
                a.GradingForm != null ? (decimal?)a.GradingForm.TotalScore : null,
                a.GradingForm != null ? a.GradingForm.SubmittedAt : null,
                a.CreatedAt))
            .ToListAsync(ct);
    }

    public void Add(GradingAssignment assignment) => _db.GradingAssignments.Add(assignment);

    public void AddForm(GradingForm form) => _db.GradingForms.Add(form);

    public void Remove(GradingAssignment assignment) => _db.GradingAssignments.Remove(assignment);
}
