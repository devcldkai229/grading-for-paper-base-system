using System.Text.Json;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using GradingService.Domain.Entities;
using GradingService.Domain.Enums;
using GradingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GradingService.Infrastructure.Services;

public class GradingSessionService : IGradingSessionService
{
    private readonly GradingDbContext _db;
    private readonly ISubmissionServiceClient _submissionClient;
    private readonly IExamCatalogServiceClient _catalogClient;

    public GradingSessionService(
        GradingDbContext db,
        ISubmissionServiceClient submissionClient,
        IExamCatalogServiceClient catalogClient)
    {
        _db = db;
        _submissionClient = submissionClient;
        _catalogClient = catalogClient;
    }

    public async Task<StartBatchResultDto?> StartBatchAsync(Guid batchId, Guid teacherId, CancellationToken ct = default)
    {
        var batch = await _submissionClient.GetBatchPapersAsync(batchId, ct);
        if (batch is null || batch.Papers.Count == 0) return null;
        if (batch.UploadedBy != teacherId) return null;

        var grid = await _catalogClient.GetGradingGridAsync(batch.SubjectId, ct);
        if (grid is null) return null;

        var summaries = new List<AssignmentSummaryDto>();

        foreach (var paper in batch.Papers)
        {
            var assignment = await EnsureAssignmentAsync(
                paper.PaperId, batch.SubjectId, teacherId, grid, ct);

            summaries.Add(new AssignmentSummaryDto(
                assignment.Id,
                paper.PaperId,
                paper.AliasNumber,
                ToStatusLabel(assignment.Status)));
        }

        var ordered = summaries
            .OrderBy(s => s.AliasNumber ?? int.MaxValue)
            .ToList();

        var resumeAssignmentId = await ResolveResumeAssignmentIdAsync(
            batchId, teacherId, ordered, ct);

        return new StartBatchResultDto(
            resumeAssignmentId,
            ordered.Select(s => s.AssignmentId).ToList(),
            batch.SubjectId,
            batchId,
            ordered);
    }

    public async Task<GradingSessionDto?> GetSessionAsync(Guid assignmentId, Guid teacherId, CancellationToken ct = default)
    {
        var assignment = await _db.GradingAssignments
            .AsNoTracking()
            .Include(a => a.GradingForm!)
                .ThenInclude(f => f.QuestionGradeDetails)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.TeacherId == teacherId, ct);

        if (assignment?.GradingForm is null) return null;

        var grid = await _catalogClient.GetGradingGridAsync(assignment.SubjectId, ct);
        if (grid is null) return null;

        var paper = await _submissionClient.GetPaperSummaryAsync(assignment.StudentPaperId, ct);
        if (paper is null) return null;

        await UpsertResumePointerAsync(teacherId, paper.BatchId, assignmentId, ct);

        var questions = assignment.GradingForm.QuestionGradeDetails
            .OrderBy(q => q.OrderIndex)
            .ThenBy(q => q.QuestionNumber)
            .Select(q => new QuestionMarkDto(
                q.QuestionNumber,
                q.GroupLabel,
                q.Label,
                q.MaxScore,
                q.Score,
                q.QuestionComment,
                q.OrderIndex))
            .ToList();

        return new GradingSessionDto(
            assignment.Id,
            assignment.StudentPaperId,
            assignment.SubjectId,
            paper.BatchId,
            grid.MaxScore,
            grid.RubricVersion,
            ToStatusLabel(assignment.Status),
            assignment.GradingForm.RowVersion,
            assignment.GradingForm.PaperComment,
            assignment.GradingForm.InternalComment,
            paper?.StudentAlias,
            paper?.AliasNumber,
            questions);
    }

    public async Task<(SaveMarksResultDto? Result, bool Conflict)> SaveMarksAsync(
        Guid assignmentId, Guid teacherId, SaveMarksRequest request, CancellationToken ct = default)
    {
        var assignment = await _db.GradingAssignments
            .Include(a => a.GradingForm!)
                .ThenInclude(f => f.QuestionGradeDetails)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.TeacherId == teacherId, ct);

        if (assignment?.GradingForm is null) return (null, false);
        if (assignment.Status == GradingProgressStatus.Submitted) return (null, false);

        var form = assignment.GradingForm;
        if (form.RowVersion != request.RowVersion) return (null, true);

        var oldSnapshot = SerializeForm(form);

        foreach (var input in request.Questions)
        {
            var detail = form.QuestionGradeDetails
                .FirstOrDefault(q => q.QuestionNumber == input.QuestionNumber);
            if (detail is null) continue;

            var clamped = Math.Clamp(input.Score, 0, detail.MaxScore);
            detail.Score = clamped;
            detail.QuestionComment = input.QuestionComment;
        }

        form.PaperComment = request.PaperComment;
        form.InternalComment = request.InternalComment;
        form.TotalScore = form.QuestionGradeDetails.Sum(q => q.Score);
        form.RowVersion++;
        assignment.Status = GradingProgressStatus.Drafting;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = teacherId,
            Action = "ScoreUpdated",
            EntityType = "GradingForm",
            EntityId = form.Id,
            OldValue = oldSnapshot,
            NewValue = SerializeForm(form)
        });

        try
        {
            await _db.SaveChangesAsync(ct);
            return (new SaveMarksResultDto(form.RowVersion), false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (null, true);
        }
    }

    public async Task<(SubmitResultDto? Result, bool Forbidden, bool NotFound)> SubmitAsync(
        Guid assignmentId, Guid teacherId, CancellationToken ct = default)
    {
        var assignment = await _db.GradingAssignments
            .Include(a => a.GradingForm)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.TeacherId == teacherId, ct);

        if (assignment is null) return (null, false, true);
        if (assignment.Status == GradingProgressStatus.Submitted) return (null, true, false);
        if (assignment.GradingForm is null) return (null, false, true);

        var paper = await _submissionClient.GetPaperSummaryAsync(assignment.StudentPaperId, ct);
        if (paper is null) return (null, false, true);

        var now = DateTime.UtcNow;
        assignment.Status = GradingProgressStatus.Submitted;
        assignment.GradingForm.SubmittedAt = now;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = teacherId,
            Action = "FormSubmitted",
            EntityType = "GradingForm",
            EntityId = assignment.GradingForm.Id,
            NewValue = JsonSerializer.Serialize(new { assignment.GradingForm.TotalScore })
        });

        await _db.SaveChangesAsync(ct);

        var batch = await _submissionClient.GetBatchPapersAsync(paper.BatchId, ct);
        Guid? nextId = null;
        if (batch is not null)
        {
            var paperIds = batch.Papers.Select(p => p.PaperId).ToList();
            var assignments = await _db.GradingAssignments
                .AsNoTracking()
                .Where(a => a.TeacherId == teacherId && paperIds.Contains(a.StudentPaperId))
                .ToListAsync(ct);

            var ordered = batch.Papers
                .OrderBy(p => p.AliasNumber ?? int.MaxValue)
                .Select(p =>
                {
                    var a = assignments.FirstOrDefault(x => x.StudentPaperId == p.PaperId);
                    return a is null ? null : new { a.Id, a.Status, p.AliasNumber };
                })
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList();

            var currentIndex = ordered.FindIndex(x => x.Id == assignmentId);
            if (currentIndex >= 0)
            {
                for (var i = currentIndex + 1; i < ordered.Count; i++)
                {
                    if (ordered[i].Status != GradingProgressStatus.Submitted)
                    {
                        nextId = ordered[i].Id;
                        break;
                    }
                }
            }
        }

        return (new SubmitResultDto(nextId), false, false);
    }

    private async Task<GradingAssignment> EnsureAssignmentAsync(
        Guid paperId, Guid subjectId, Guid teacherId,
        SubjectGradingGridClientDto grid, CancellationToken ct)
    {
        var existing = await _db.GradingAssignments
            .Include(a => a.GradingForm!)
                .ThenInclude(f => f.QuestionGradeDetails)
            .FirstOrDefaultAsync(a =>
                a.StudentPaperId == paperId
                && a.TeacherId == teacherId
                && a.AssignmentType == AssignmentType.FirstGrade, ct);

        if (existing is not null)
        {
            await SeedMissingQuestionsAsync(existing, grid, ct);
            return existing;
        }

        var assignment = new GradingAssignment
        {
            StudentPaperId = paperId,
            SubjectId = subjectId,
            TeacherId = teacherId,
            AssignmentType = AssignmentType.FirstGrade,
            Status = GradingProgressStatus.NotStarted
        };

        var form = new GradingForm
        {
            GradingAssignmentId = assignment.Id,
            TotalScore = 0,
            RowVersion = 0
        };

        foreach (var q in grid.Questions.OrderBy(x => x.OrderIndex))
        {
            form.QuestionGradeDetails.Add(new QuestionGradeDetail
            {
                GradingFormId = form.Id,
                QuestionNumber = q.QuestionNumber,
                GroupLabel = q.GroupLabel,
                Label = q.Label,
                OrderIndex = q.OrderIndex,
                Score = 0,
                MaxScore = q.MaxScore,
                AiDrafted = false
            });
        }

        assignment.GradingForm = form;
        _db.GradingAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);

        return assignment;
    }

    private async Task SeedMissingQuestionsAsync(
        GradingAssignment assignment, SubjectGradingGridClientDto grid, CancellationToken ct)
    {
        if (assignment.GradingForm is null)
        {
            assignment.GradingForm = new GradingForm
            {
                GradingAssignmentId = assignment.Id,
                TotalScore = 0,
                RowVersion = 0
            };
            _db.GradingForms.Add(assignment.GradingForm);
        }

        var existingNumbers = assignment.GradingForm.QuestionGradeDetails
            .Select(q => q.QuestionNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = false;
        foreach (var q in grid.Questions.OrderBy(x => x.OrderIndex))
        {
            if (existingNumbers.Contains(q.QuestionNumber)) continue;

            assignment.GradingForm.QuestionGradeDetails.Add(new QuestionGradeDetail
            {
                GradingFormId = assignment.GradingForm.Id,
                QuestionNumber = q.QuestionNumber,
                GroupLabel = q.GroupLabel,
                Label = q.Label,
                OrderIndex = q.OrderIndex,
                Score = 0,
                MaxScore = q.MaxScore,
                AiDrafted = false
            });
            added = true;
        }

        if (added) await _db.SaveChangesAsync(ct);
    }

    private async Task<Guid> ResolveResumeAssignmentIdAsync(
        Guid batchId,
        Guid teacherId,
        IReadOnlyList<AssignmentSummaryDto> ordered,
        CancellationToken ct)
    {
        if (ordered.Count == 0)
            throw new InvalidOperationException("Batch has no assignments.");

        var assignmentIds = ordered.Select(o => o.AssignmentId).ToHashSet();

        var pointer = await _db.GradingResumePointers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TeacherId == teacherId && p.BatchId == batchId, ct);

        if (pointer is not null && assignmentIds.Contains(pointer.AssignmentId))
        {
            var saved = ordered.First(o => o.AssignmentId == pointer.AssignmentId);
            if (saved.Status != "Submitted")
                return pointer.AssignmentId;
        }

        var drafting = ordered.FirstOrDefault(o => o.Status == "Drafting");
        if (drafting is not null)
            return drafting.AssignmentId;

        var notStarted = ordered.FirstOrDefault(o => o.Status == "NotStarted");
        if (notStarted is not null)
            return notStarted.AssignmentId;

        return ordered[0].AssignmentId;
    }

    private async Task UpsertResumePointerAsync(
        Guid teacherId,
        Guid batchId,
        Guid assignmentId,
        CancellationToken ct)
    {
        var existing = await _db.GradingResumePointers
            .FirstOrDefaultAsync(p => p.TeacherId == teacherId && p.BatchId == batchId, ct);

        if (existing is null)
        {
            _db.GradingResumePointers.Add(new GradingResumePointer
            {
                TeacherId = teacherId,
                BatchId = batchId,
                AssignmentId = assignmentId
            });
        }
        else
        {
            existing.UpdateAssignment(assignmentId);
        }

        await _db.SaveChangesAsync(ct);
    }

    private static string ToStatusLabel(GradingProgressStatus status) =>
        status switch
        {
            GradingProgressStatus.Submitted => "Submitted",
            GradingProgressStatus.Drafting => "Drafting",
            _ => "NotStarted"
        };

    private static string SerializeForm(GradingForm form) =>
        JsonSerializer.Serialize(new
        {
            form.TotalScore,
            form.PaperComment,
            form.InternalComment,
            form.RowVersion,
            Questions = form.QuestionGradeDetails.Select(q => new
            {
                q.QuestionNumber,
                q.Score,
                q.QuestionComment
            })
        });
}
