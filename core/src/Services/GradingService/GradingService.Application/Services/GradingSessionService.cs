using System.Text.Json;
using Contracts.Messages;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using GradingService.Domain.Entities;
using GradingService.Domain.Enums;
using GradingService.Domain.Exceptions;

namespace GradingService.Application.Services;

public class GradingSessionService : IGradingSessionService
{
    private readonly IGradingAssignmentRepository _assignments;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IGradingResumePointerRepository _resumePointers;
    private readonly IUnitOfWork _uow;
    private readonly ISubmissionServiceClient _submissionClient;
    private readonly IExamCatalogServiceClient _catalogClient;
    private readonly IGradeExportFileBuilder _exportBuilder;
    private readonly IMarkerAssignmentRepository _markerAssignments;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IIamServiceClient? _iamClient;

    public GradingSessionService(
        IGradingAssignmentRepository assignments,
        IAuditLogRepository auditLogs,
        IGradingResumePointerRepository resumePointers,
        IUnitOfWork uow,
        ISubmissionServiceClient submissionClient,
        IExamCatalogServiceClient catalogClient,
        IGradeExportFileBuilder exportBuilder,
        IMarkerAssignmentRepository markerAssignments,
        IMessagePublisher messagePublisher,
        IIamServiceClient? iamClient = null)
    {
        _assignments = assignments;
        _auditLogs = auditLogs;
        _resumePointers = resumePointers;
        _uow = uow;
        _submissionClient = submissionClient;
        _catalogClient = catalogClient;
        _exportBuilder = exportBuilder;
        _markerAssignments = markerAssignments;
        _messagePublisher = messagePublisher;
        _iamClient = iamClient;
    }

    public async Task<(StartBatchResultDto? Result, string? Error)> StartBatchAsync(
        Guid batchId, Guid teacherId, CancellationToken ct = default)
    {
        var batch = await _submissionClient.GetBatchPapersAsync(batchId, ct);
        if (batch is null || batch.Papers.Count == 0) return (null, null);
        if (batch.UploadedBy != teacherId) return (null, null);

        var grid = await _catalogClient.GetGradingGridAsync(batch.SubjectId, ct);
        if (grid is null) return (null, null);

        if (string.Equals(grid.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            return (null, "This subject is closed; new grading sessions can no longer be started.");
        }

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

        return (new StartBatchResultDto(
            resumeAssignmentId,
            ordered.Select(s => s.AssignmentId).ToList(),
            batch.SubjectId,
            batchId,
            ordered), null);
    }

    public async Task<GradingSessionDto?> GetSessionAsync(Guid assignmentId, Guid teacherId, CancellationToken ct = default)
    {
        var assignment = await _assignments.GetWithFormAndDetailsAsync(assignmentId, teacherId, asNoTracking: false, ct);
        if (assignment?.GradingForm is null) return null;

        var grid = await _catalogClient.GetGradingGridAsync(assignment.SubjectId, ct);
        if (grid is null) return null;

        await SeedMissingQuestionsAsync(assignment, grid, ct);

        var dto = await BuildSessionDtoAsync(assignment, grid, ct);
        if (dto is null) return null;

        await UpsertResumePointerAsync(teacherId, dto.BatchId, assignmentId, ct);
        return dto;
    }

    public async Task<GradingSessionDto?> GetAssignmentForOverrideAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await _assignments.GetWithFormAndDetailsAsync(assignmentId, null, asNoTracking: false, ct);
        if (assignment?.GradingForm is null) return null;

        var grid = await _catalogClient.GetGradingGridAsync(assignment.SubjectId, ct);
        if (grid is null) return null;

        await SeedMissingQuestionsAsync(assignment, grid, ct);

        return await BuildSessionDtoAsync(assignment, grid, ct);
    }

    private async Task<GradingSessionDto?> BuildSessionDtoAsync(
        GradingAssignment assignment, SubjectGradingGridClientDto grid, CancellationToken ct)
    {
        var paper = await _submissionClient.GetPaperSummaryAsync(assignment.StudentPaperId, ct);
        if (paper is null) return null;

        var questions = assignment.GradingForm!.QuestionGradeDetails
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

    public async Task<(SaveMarksResultDto? Result, bool Conflict, string? Error)> SaveMarksAsync(
        Guid assignmentId, Guid teacherId, SaveMarksRequest request, CancellationToken ct = default)
    {
        var assignment = await _assignments.GetWithFormAndDetailsAsync(assignmentId, teacherId, asNoTracking: false, ct);
        if (assignment?.GradingForm is null) return (null, false, null);
        if (assignment.Status == GradingProgressStatus.Submitted) return (null, false, "Session is already submitted");

        var form = assignment.GradingForm;
        if (form.RowVersion != request.RowVersion) return (null, true, null);

        // Validation: Scores validated 0..max per leaf
        var hasChanges = false;
        foreach (var input in request.Questions)
        {
            var detail = form.QuestionGradeDetails
                .FirstOrDefault(q => q.QuestionNumber == input.QuestionNumber);
            if (detail is null) continue;

            if (input.Score < 0 || input.Score > detail.MaxScore)
            {
                return (null, false, $"Score for question {input.QuestionNumber} must be between 0 and {detail.MaxScore}.");
            }

            if (detail.Score != input.Score || detail.QuestionComment != input.QuestionComment)
            {
                hasChanges = true;
            }
        }

        if (form.PaperComment != request.PaperComment || form.InternalComment != request.InternalComment)
        {
            hasChanges = true;
        }

        if (!hasChanges)
        {
            return (new SaveMarksResultDto(form.RowVersion), false, null);
        }

        var oldSnapshot = SerializeForm(form);

        foreach (var input in request.Questions)
        {
            var detail = form.QuestionGradeDetails
                .FirstOrDefault(q => q.QuestionNumber == input.QuestionNumber);
            if (detail is null) continue;

            detail.Score = input.Score;
            detail.QuestionComment = input.QuestionComment;
        }

        form.PaperComment = request.PaperComment;
        form.InternalComment = request.InternalComment;
        form.TotalScore = form.QuestionGradeDetails.Sum(q => q.Score);
        form.RowVersion++;
        assignment.Status = GradingProgressStatus.Drafting;

        _auditLogs.Add(new AuditLog
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
            await _uow.SaveChangesAsync(ct);
            return (new SaveMarksResultDto(form.RowVersion), false, null);
        }
        catch (ConcurrencyConflictException)
        {
            return (null, true, null);
        }
    }

    private const int MaxHeartbeatSeconds = 60;

    public async Task<bool> RecordHeartbeatAsync(
        Guid assignmentId, Guid teacherId, int seconds, CancellationToken ct = default)
    {
        var assignment = await _assignments.GetWithFormAsync(assignmentId, teacherId, asNoTracking: false, ct);
        if (assignment?.GradingForm is null) return false;
        if (assignment.Status == GradingProgressStatus.Submitted) return false;

        var clamped = Math.Clamp(seconds, 0, MaxHeartbeatSeconds);
        assignment.GradingForm.ActiveSecondsSpent += clamped;
        assignment.GradingForm.LastActiveAt = DateTime.UtcNow;

        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // A concurrent SaveMarks/Submit already changed RowVersion on this row; this
            // heartbeat tick is analytics-only, so it's fine to drop it rather than retry.
        }

        return true;
    }

    public async Task<(OverrideMarksResultDto? Result, bool NotFound, string? Error)> OverrideMarksAsync(
        Guid assignmentId, Guid actingUserId, bool isAdmin, OverrideMarksRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return (null, false, "A reason is required to override scores.");
        }

        Guid? teacherFilter = isAdmin ? null : actingUserId;
        var assignment = await _assignments.GetWithFormAndDetailsAsync(assignmentId, teacherFilter, asNoTracking: false, ct);
        if (assignment?.GradingForm is null) return (null, true, null);

        var form = assignment.GradingForm;

        // Validation: Scores validated 0..max per leaf (same rule as a normal save).
        foreach (var input in request.Questions)
        {
            var detail = form.QuestionGradeDetails
                .FirstOrDefault(q => q.QuestionNumber == input.QuestionNumber);
            if (detail is null) continue;

            if (input.Score < 0 || input.Score > detail.MaxScore)
            {
                return (null, false, $"Score for question {input.QuestionNumber} must be between 0 and {detail.MaxScore}.");
            }
        }

        var oldSnapshot = SerializeForm(form);

        foreach (var input in request.Questions)
        {
            var detail = form.QuestionGradeDetails
                .FirstOrDefault(q => q.QuestionNumber == input.QuestionNumber);
            if (detail is null) continue;

            detail.Score = input.Score;
            detail.QuestionComment = input.QuestionComment;
        }

        form.PaperComment = request.PaperComment;
        form.InternalComment = request.InternalComment;
        form.TotalScore = form.QuestionGradeDetails.Sum(q => q.Score);
        form.RowVersion++;

        _auditLogs.Add(new AuditLog
        {
            UserId = actingUserId,
            Action = "ScoreOverridden",
            EntityType = "GradingForm",
            EntityId = form.Id,
            OldValue = oldSnapshot,
            NewValue = SerializeForm(form),
            Reason = request.Reason
        });

        await _uow.SaveChangesAsync(ct);

        // Only notify when someone other than the original marker made the change — the marker
        // doesn't need to be told about their own edit.
        if (actingUserId != assignment.TeacherId)
        {
            await _messagePublisher.PublishAsync(new RegradeNotificationEvent(
                Guid.NewGuid(), assignment.TeacherId, assignment.Id, assignment.SubjectId,
                request.Reason, DateTime.UtcNow), ct);
        }

        return (new OverrideMarksResultDto(form.RowVersion, form.TotalScore), false, null);
    }

    public async Task<(IReadOnlyList<AuditLogEntryDto>? Result, bool NotFound)> GetAuditTrailAsync(
        Guid assignmentId, Guid actingUserId, bool isAdmin, CancellationToken ct = default)
    {
        Guid? teacherFilter = isAdmin ? null : actingUserId;
        var assignment = await _assignments.GetWithFormAsync(assignmentId, teacherFilter, asNoTracking: true, ct);
        if (assignment?.GradingForm is null) return (null, true);

        var entries = await _auditLogs.ListForEntityAsync("GradingForm", assignment.GradingForm.Id, ct);
        var mapped = entries
            .Select(l => new AuditLogEntryDto(
                l.Id, l.UserId, l.Action, l.OldValue, l.NewValue, l.Reason, l.CreatedAt))
            .ToList();

        return (mapped, false);
    }

    public async Task<(SubmitResultDto? Result, bool Forbidden, bool NotFound)> SubmitAsync(
        Guid assignmentId, Guid teacherId, CancellationToken ct = default)
    {
        var assignment = await _assignments.GetWithFormAsync(assignmentId, teacherId, asNoTracking: false, ct);

        if (assignment is null) return (null, false, true);
        if (assignment.Status == GradingProgressStatus.Submitted) return (null, true, false);
        if (assignment.GradingForm is null) return (null, false, true);

        var paper = await _submissionClient.GetPaperSummaryAsync(assignment.StudentPaperId, ct);
        if (paper is null) return (null, false, true);

        var now = DateTime.UtcNow;
        assignment.Status = GradingProgressStatus.Submitted;
        assignment.GradingForm.SubmittedAt = now;

        _auditLogs.Add(new AuditLog
        {
            UserId = teacherId,
            Action = "FormSubmitted",
            EntityType = "GradingForm",
            EntityId = assignment.GradingForm.Id,
            NewValue = JsonSerializer.Serialize(new { assignment.GradingForm.TotalScore })
        });

        await _uow.SaveChangesAsync(ct);

        var batch = await _submissionClient.GetBatchPapersAsync(paper.BatchId, ct);
        Guid? nextId = null;
        if (batch is not null)
        {
            var paperIds = batch.Papers.Select(p => p.PaperId).ToList();
            var assignments = await _assignments.ListByTeacherAndPaperIdsAsync(teacherId, paperIds, ct);

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
        var existing = await _assignments.FindFirstGradeForPaperAsync(paperId, teacherId, ct);

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
        _assignments.Add(assignment);
        await _uow.SaveChangesAsync(ct);

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
            _assignments.AddForm(assignment.GradingForm);
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

        if (added) await _uow.SaveChangesAsync(ct);
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

        var pointer = await _resumePointers.GetAsync(teacherId, batchId, asNoTracking: true, ct);

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
        var existing = await _resumePointers.GetAsync(teacherId, batchId, asNoTracking: false, ct);

        if (existing is null)
        {
            _resumePointers.Add(new GradingResumePointer
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

        await _uow.SaveChangesAsync(ct);
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

    public async Task<byte[]?> ExportGradesAsync(Guid subjectId, Guid requestedBy, CancellationToken ct = default)
    {
        var assignments = await _assignments.ListBySubjectWithFormsAsync(subjectId, submittedOnly: false, ct);
        if (assignments.Count == 0) return null;

        // Fetch lecturer marker codes
        Dictionary<Guid, string> teacherMarkerCodes = new();
        if (_iamClient != null)
        {
            teacherMarkerCodes = await _iamClient.GetAllLecturerMarkerCodesAsync(ct);
        }

        // Fetch paper summaries in parallel to get student alias & alias numbers
        var paperTasks = assignments.Select(async a =>
        {
            var summary = await _submissionClient.GetPaperSummaryAsync(a.StudentPaperId, ct);
            return new { PaperId = a.StudentPaperId, Summary = summary };
        });
        var paperSummaries = await Task.WhenAll(paperTasks);
        var paperMap = paperSummaries
            .Where(x => x.Summary is not null)
            .ToDictionary(x => x.PaperId, x => x.Summary!);

        // Extract and logically order all unique question numbers by their order index, alongside their max scores
        var questionDetails = assignments
            .SelectMany(a => a.GradingForm?.QuestionGradeDetails ?? Enumerable.Empty<QuestionGradeDetail>())
            .GroupBy(q => q.QuestionNumber)
            .Select(g => new { QuestionNumber = g.Key, OrderIndex = g.Min(q => q.OrderIndex), MaxScore = g.Max(q => q.MaxScore) })
            .OrderBy(x => x.OrderIndex)
            .ThenBy(x => x.QuestionNumber)
            .ToList();

        var rows = new List<Dictionary<string, object>>();

        // Header Row 1: empty A & B, "Question X", "Total", empty
        var row1 = new Dictionary<string, object>
        {
            ["Col0"] = "",
            ["Col1"] = ""
        };
        for (int i = 0; i < questionDetails.Count; i++)
        {
            row1[$"Col{i + 2}"] = "Question " + questionDetails[i].QuestionNumber;
        }
        row1[$"Col{questionDetails.Count + 2}"] = "Total";
        row1[$"Col{questionDetails.Count + 3}"] = "";
        rows.Add(row1);

        // Header Row 2: "Alias", "Marker", max_q1, ..., max_qN, total_max, "Comment"
        var row2 = new Dictionary<string, object>
        {
            ["Col0"] = "Alias",
            ["Col1"] = "Marker"
        };
        for (int i = 0; i < questionDetails.Count; i++)
        {
            row2[$"Col{i + 2}"] = questionDetails[i].MaxScore;
        }
        var totalMaxScore = questionDetails.Sum(x => x.MaxScore);
        row2[$"Col{questionDetails.Count + 2}"] = totalMaxScore;
        row2[$"Col{questionDetails.Count + 3}"] = "Comment";
        rows.Add(row2);

        // Student Data Rows
        var sortedAssignments = assignments
            .OrderBy(a => paperMap.TryGetValue(a.StudentPaperId, out var paper) ? (paper.AliasNumber ?? int.MaxValue) : int.MaxValue)
            .ToList();

        foreach (var a in sortedAssignments)
        {
            paperMap.TryGetValue(a.StudentPaperId, out var paper);
            teacherMarkerCodes.TryGetValue(a.TeacherId, out var markerCode);
            if (string.IsNullOrEmpty(markerCode))
            {
                markerCode = "Lecturer";
            }

            var studentRow = new Dictionary<string, object>
            {
                ["Col0"] = paper?.AliasNumber != null ? (object)paper.AliasNumber : "",
                ["Col1"] = markerCode
            };

            for (int i = 0; i < questionDetails.Count; i++)
            {
                var q = questionDetails[i];
                var scoreDetail = a.GradingForm?.QuestionGradeDetails
                    .FirstOrDefault(x => x.QuestionNumber == q.QuestionNumber);
                studentRow[$"Col{i + 2}"] = scoreDetail != null ? (object)scoreDetail.Score : 0;
            }

            studentRow[$"Col{questionDetails.Count + 2}"] = a.GradingForm?.TotalScore ?? 0;
            studentRow[$"Col{questionDetails.Count + 3}"] = a.GradingForm?.PaperComment ?? "";

            rows.Add(studentRow);
        }

        var bytes = _exportBuilder.Build(rows, printHeader: false);

        await _messagePublisher.PublishAsync(new ExportReadyEvent(
            Guid.NewGuid(), requestedBy, subjectId, DateTime.UtcNow), ct);

        return bytes;
    }

    public async Task<MyProgressDto> GetMyProgressAsync(Guid teacherId, CancellationToken ct = default)
    {
        var assignments = await _assignments.ListProgressByTeacherAsync(teacherId, ct);

        var total = assignments.Count;
        var submitted = assignments.Count(a => a.Status == GradingProgressStatus.Submitted);
        var remaining = total - submitted;

        var subjectsWithRemainingWork = assignments
            .GroupBy(a => a.SubjectId)
            .Select(g => new
            {
                SubjectId = g.Key,
                Total = g.Count(),
                Submitted = g.Count(a => a.Status == GradingProgressStatus.Submitted),
                NextAssignmentId = g
                    .Where(a => a.Status != GradingProgressStatus.Submitted)
                    .OrderBy(a => a.CreatedAt)
                    .Select(a => (Guid?)a.Id)
                    .FirstOrDefault()
            })
            .Where(g => g.Submitted < g.Total)
            .ToList();

        var deadlineTasks = subjectsWithRemainingWork.Select(async s =>
        {
            var info = await _catalogClient.GetExamInfoAsync(s.SubjectId, ct);
            return new UpcomingDeadlineDto(
                s.SubjectId,
                info?.SubjectCode ?? "Unknown",
                info?.ExamName ?? "N/A",
                info?.ExamEndDate ?? new DateOnly(9999, 12, 31),
                s.Total,
                s.Submitted,
                s.Total - s.Submitted,
                s.NextAssignmentId);
        });

        var upcomingDeadlines = (await Task.WhenAll(deadlineTasks))
            .OrderBy(d => d.ExamEndDate)
            .ToList();

        var nextAssignmentId = upcomingDeadlines.FirstOrDefault()?.NextAssignmentId
            ?? subjectsWithRemainingWork.Select(s => s.NextAssignmentId).FirstOrDefault(id => id.HasValue);

        return new MyProgressDto(total, submitted, remaining, nextAssignmentId, upcomingDeadlines);
    }

    // Rolling window used to compute "papers/hour" throughput and, from it, an ETA.
    private static readonly TimeSpan ThroughputWindow = TimeSpan.FromHours(24);

    public async Task<GradingProgressDashboardDto> GetProgressDashboardAsync(
        IReadOnlyCollection<Guid>? subjectIds, CancellationToken ct = default)
    {
        var rows = await _assignments.ListProgressRowsAsync(subjectIds, ct);

        var now = DateTime.UtcNow;
        var windowStart = now - ThroughputWindow;

        var subjects = rows
            .GroupBy(r => r.SubjectId)
            .Select(subjectGroup =>
            {
                var lecturers = subjectGroup
                    .GroupBy(r => r.TeacherId)
                    .Select(teacherGroup => BuildLecturerProgress(teacherGroup.Key, teacherGroup.ToList(), now, windowStart))
                    .OrderByDescending(l => l.AssignedCount)
                    .ToList();

                var total = subjectGroup.Count();
                var completed = subjectGroup.Count(r => r.Status == GradingProgressStatus.Submitted);
                var drafting = subjectGroup.Count(r => r.Status == GradingProgressStatus.Drafting);
                var notStarted = total - completed - drafting;
                var scores = subjectGroup
                    .Where(r => r.Status == GradingProgressStatus.Submitted && r.TotalScore.HasValue)
                    .Select(r => r.TotalScore!.Value)
                    .ToList();

                // Subject-level throughput = combined rate of every lecturer working on it.
                var throughput = lecturers.Sum(l => l.ThroughputPerHour ?? 0);
                var remaining = total - completed;
                DateTime? estimatedFinish = throughput > 0 && remaining > 0
                    ? now.AddHours((double)(remaining / throughput))
                    : null;

                return new SubjectProgressDto(
                    subjectGroup.Key,
                    total,
                    completed,
                    drafting,
                    notStarted,
                    total == 0 ? 0 : Math.Round(100m * completed / total, 1),
                    scores.Count > 0 ? Math.Round(scores.Average(), 2) : null,
                    scores.Count > 0 ? scores.Min() : null,
                    scores.Count > 0 ? scores.Max() : null,
                    throughput > 0 ? Math.Round(throughput, 2) : null,
                    estimatedFinish,
                    lecturers,
                    ComputeAvgGradingMinutesPerPaper(subjectGroup));
            })
            .OrderBy(s => s.CompletionPercent)
            .ToList();

        return new GradingProgressDashboardDto(subjects);
    }

    /// <summary>Average grading time (minutes) per paper, over Submitted papers only — an
    /// in-progress paper's still-accumulating time would skew the average down.</summary>
    private static decimal? ComputeAvgGradingMinutesPerPaper(IEnumerable<SubjectProgressRow> rows)
    {
        var submittedSeconds = rows
            .Where(r => r.Status == GradingProgressStatus.Submitted)
            .Select(r => r.ActiveSecondsSpent)
            .ToList();

        return submittedSeconds.Count > 0
            ? Math.Round((decimal)submittedSeconds.Average() / 60m, 1)
            : null;
    }

    private static LecturerProgressDto BuildLecturerProgress(
        Guid teacherId, IReadOnlyList<SubjectProgressRow> rows, DateTime now, DateTime windowStart)
    {
        var assigned = rows.Count;
        var completed = rows.Count(r => r.Status == GradingProgressStatus.Submitted);
        var drafting = rows.Count(r => r.Status == GradingProgressStatus.Drafting);
        var notStarted = assigned - completed - drafting;

        var completedScores = rows
            .Where(r => r.Status == GradingProgressStatus.Submitted && r.TotalScore.HasValue)
            .Select(r => r.TotalScore!.Value)
            .ToList();

        var submittedTimestamps = rows
            .Where(r => r.SubmittedAt.HasValue)
            .Select(r => r.SubmittedAt!.Value)
            .ToList();

        var lastActivity = submittedTimestamps.Count > 0 ? submittedTimestamps.Max() : (DateTime?)null;

        var completedInWindow = submittedTimestamps.Count(t => t >= windowStart);
        decimal? throughput = completedInWindow > 0
            ? Math.Round(completedInWindow / (decimal)ThroughputWindow.TotalHours, 2)
            : null;

        var remaining = assigned - completed;
        DateTime? estimatedFinish = throughput is > 0 && remaining > 0
            ? now.AddHours((double)(remaining / throughput.Value))
            : null;

        return new LecturerProgressDto(
            teacherId, assigned, completed, drafting, notStarted,
            completedScores.Count > 0 ? Math.Round(completedScores.Average(), 2) : null,
            throughput, lastActivity, estimatedFinish,
            ComputeAvgGradingMinutesPerPaper(rows));
    }

    public async Task<ScoreDistributionDashboardDto> GetScoreDistributionAsync(
        IReadOnlyCollection<Guid>? subjectIds, CancellationToken ct = default)
    {
        var rows = await _assignments.ListSubmittedScoresAsync(subjectIds, ct);

        var subjects = rows
            .GroupBy(r => r.SubjectId)
            .Select(g =>
            {
                var scores = g.Select(r => r.Score).ToList();
                return new SubjectScoreDistributionDto(
                    g.Key,
                    scores.Count,
                    Math.Round(scores.Average(), 2),
                    scores.Min(),
                    scores.Max(),
                    scores);
            })
            .ToList();

        return new ScoreDistributionDashboardDto(subjects);
    }

    public async Task<AuditLogPageDto> GetAuditLogsAsync(
        Guid? userId, string? entityType, string? action, int page, int pageSize, CancellationToken ct = default)
    {
        var (items, totalCount) = await _auditLogs.ListAsync(userId, entityType, action, page, pageSize, ct);

        var records = items
            .Select(l => new AuditLogRecordDto(
                l.Id, l.UserId, l.Action, l.EntityType, l.EntityId, l.OldValue, l.NewValue, l.Reason, l.CreatedAt))
            .ToList();

        return new AuditLogPageDto(records, totalCount);
    }

    public async Task<SubjectFeedbackExportDto> GetReleasableFeedbackAsync(Guid subjectId, CancellationToken ct = default)
    {
        var assignments = await _assignments.ListBySubjectWithFormsAsync(subjectId, submittedOnly: true, ct);

        var paperTasks = assignments
            .Where(a => a.GradingForm is not null)
            .Select(async a =>
            {
                var summary = await _submissionClient.GetPaperSummaryAsync(a.StudentPaperId, ct);
                return (Assignment: a, Summary: summary);
            });
        var joined = await Task.WhenAll(paperTasks);

        var students = joined
            .OrderBy(x => x.Summary?.AliasNumber ?? int.MaxValue)
            .Select(x => new StudentFeedbackDto(
                x.Assignment.StudentPaperId,
                x.Summary?.AliasNumber,
                x.Summary?.StudentAlias,
                x.Assignment.GradingForm!.TotalScore,
                x.Assignment.GradingForm!.PaperComment,
                x.Assignment.GradingForm!.QuestionGradeDetails
                    .OrderBy(q => q.OrderIndex)
                    .Select(q => new QuestionFeedbackDto(q.QuestionNumber, q.Label, q.Score, q.MaxScore, q.QuestionComment))
                    .ToList()))
            .ToList();

        return new SubjectFeedbackExportDto(subjectId, students);
    }

    public async Task<IReadOnlyList<MarkerAssignmentDto>> ListMarkerAssignmentsAsync(
        Guid subjectId, CancellationToken ct = default)
    {
        var assignments = await _markerAssignments.ListForSubjectAsync(subjectId, ct);
        return assignments.Select(ToDto).ToList();
    }

    public async Task<(MarkerAssignmentDto? Result, string? Error)> CreateMarkerAssignmentAsync(
        Guid subjectId, CreateMarkerAssignmentRequest request, Guid assignedBy, CancellationToken ct = default)
    {
        var existing = await _markerAssignments.ListForSubjectAsync(subjectId, ct);

        int aliasStart, aliasEnd;

        if (request.AliasStart.HasValue && request.AliasEnd.HasValue)
        {
            aliasStart = request.AliasStart.Value;
            aliasEnd = request.AliasEnd.Value;

            if (aliasStart < 1 || aliasEnd < aliasStart)
            {
                return (null, "Alias range is invalid: start must be >= 1 and end must be >= start.");
            }

            if (existing.Any(m => aliasStart <= m.AliasEnd && m.AliasStart <= aliasEnd))
            {
                return (null, $"Alias range {aliasStart}-{aliasEnd} overlaps an existing assignment for this subject.");
            }
        }
        else if (request.Quota.HasValue)
        {
            if (request.Quota.Value < 1)
            {
                return (null, "Quota must be at least 1.");
            }

            aliasStart = existing.Count == 0 ? 1 : existing.Max(m => m.AliasEnd) + 1;
            aliasEnd = aliasStart + request.Quota.Value - 1;
        }
        else
        {
            return (null, "Either an alias range (AliasStart/AliasEnd) or a Quota must be provided.");
        }

        var paperValidationError = await ValidateAgainstSubmittedPapersAsync(subjectId, aliasEnd, ct);
        if (paperValidationError is not null)
        {
            return (null, paperValidationError);
        }

        var assignment = new MarkerAssignment
        {
            SubjectId = subjectId,
            TeacherId = request.TeacherId,
            AliasStart = aliasStart,
            AliasEnd = aliasEnd,
            AssignedBy = assignedBy
        };

        _markerAssignments.Add(assignment);
        await _uow.SaveChangesAsync(ct);

        await PublishAssignmentNotificationAsync(assignment, ct);

        return (ToDto(assignment), null);
    }

    public async Task<(MarkerAssignmentDto? Result, bool NotFound, string? Error)> ReassignMarkerAssignmentAsync(
        Guid id, ReassignMarkerAssignmentRequest request, Guid assignedBy, CancellationToken ct = default)
    {
        var assignment = await _markerAssignments.GetByIdAsync(id, ct);
        if (assignment is null) return (null, true, null);

        if (request.AliasStart < 1 || request.AliasEnd < request.AliasStart)
        {
            return (null, false, "Alias range is invalid: start must be >= 1 and end must be >= start.");
        }

        var existing = await _markerAssignments.ListForSubjectAsync(assignment.SubjectId, ct);
        var overlaps = existing.Any(m =>
            m.Id != id && request.AliasStart <= m.AliasEnd && m.AliasStart <= request.AliasEnd);
        if (overlaps)
        {
            return (null, false, $"Alias range {request.AliasStart}-{request.AliasEnd} overlaps an existing assignment for this subject.");
        }

        var paperValidationError = await ValidateAgainstSubmittedPapersAsync(assignment.SubjectId, request.AliasEnd, ct);
        if (paperValidationError is not null)
        {
            return (null, false, paperValidationError);
        }

        assignment.TeacherId = request.TeacherId;
        assignment.AliasStart = request.AliasStart;
        assignment.AliasEnd = request.AliasEnd;
        assignment.AssignedBy = assignedBy;
        assignment.AssignedAt = DateTime.UtcNow;

        await _uow.SaveChangesAsync(ct);

        await PublishAssignmentNotificationAsync(assignment, ct);

        return (ToDto(assignment), false, null);
    }

    /// <summary>Notifies the (possibly new) teacher that they now own this alias range.</summary>
    private Task PublishAssignmentNotificationAsync(MarkerAssignment assignment, CancellationToken ct) =>
        _messagePublisher.PublishAsync(new AssignmentNotificationEvent(
            Guid.NewGuid(), assignment.TeacherId, assignment.SubjectId,
            assignment.AliasStart, assignment.AliasEnd, DateTime.UtcNow), ct);

    public async Task<bool> DeleteMarkerAssignmentAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await _markerAssignments.GetByIdAsync(id, ct);
        if (assignment is null) return false;

        _markerAssignments.Remove(assignment);
        await _uow.SaveChangesAsync(ct);

        return true;
    }

    public async Task<int> RunDeadlineReminderSweepAsync(int reminderWindowDays = 3, CancellationToken ct = default)
    {
        var rows = await _assignments.ListProgressRowsAsync(null, ct);

        var pendingGroups = rows
            .GroupBy(r => (r.SubjectId, r.TeacherId))
            .Select(g => new
            {
                g.Key.SubjectId,
                g.Key.TeacherId,
                Total = g.Count(),
                Submitted = g.Count(r => r.Status == GradingProgressStatus.Submitted)
            })
            .Where(g => g.Submitted < g.Total)
            .ToList();

        if (pendingGroups.Count == 0) return 0;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var deadline = today.AddDays(reminderWindowDays);

        var subjectIds = pendingGroups.Select(g => g.SubjectId).Distinct().ToList();
        var examInfoResults = await Task.WhenAll(subjectIds.Select(async id =>
            new { SubjectId = id, Info = await _catalogClient.GetExamInfoAsync(id, ct) }));
        var examInfoBySubject = examInfoResults
            .Where(x => x.Info?.ExamEndDate is not null)
            .ToDictionary(x => x.SubjectId, x => x.Info!);

        var publishedCount = 0;
        foreach (var group in pendingGroups)
        {
            if (!examInfoBySubject.TryGetValue(group.SubjectId, out var info)) continue;
            if (info.ExamEndDate!.Value > deadline) continue;

            var remaining = group.Total - group.Submitted;
            var messageId = DeterministicDailyMessageId(group.SubjectId, group.TeacherId, today);

            await _messagePublisher.PublishAsync(new DeadlineReminderEvent(
                messageId, group.TeacherId, group.SubjectId, info.SubjectCode,
                info.ExamEndDate.Value, remaining, DateTime.UtcNow), ct);

            publishedCount++;
        }

        return publishedCount;
    }

    /// <summary>
    /// Same (subject, teacher, calendar day) always maps to the same id, so re-running the sweep
    /// several times on the same day publishes a MessageId the NotificationService consumer has
    /// already claimed (via its 24h Redis dedup key) — one reminder per pair per day, not one per tick.
    /// </summary>
    private static Guid DeterministicDailyMessageId(Guid subjectId, Guid teacherId, DateOnly day)
    {
        var seed = $"{subjectId:N}:{teacherId:N}:{day:yyyy-MM-dd}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(seed));
        return new Guid(hash);
    }

    /// <summary>
    /// Rejects an alias range that has nothing to back it: either the subject has no submitted
    /// papers at all, or AliasEnd reaches past the highest alias number actually submitted.
    /// Returns null (no error) when SubmissionService is unreachable — an admin allocation
    /// shouldn't be blocked by a transient dependency outage.
    /// </summary>
    private async Task<string?> ValidateAgainstSubmittedPapersAsync(Guid subjectId, int aliasEnd, CancellationToken ct)
    {
        var stats = await _submissionClient.GetSubjectPaperStatsAsync(subjectId, ct);
        if (stats is null) return null;

        if (stats.TotalPapers == 0)
        {
            return "This subject has no submitted papers yet; there is nothing to allocate.";
        }

        if (stats.MaxAliasNumber.HasValue && aliasEnd > stats.MaxAliasNumber.Value)
        {
            return $"Alias end {aliasEnd} exceeds the highest submitted alias number ({stats.MaxAliasNumber.Value}) for this subject.";
        }

        return null;
    }

    private static MarkerAssignmentDto ToDto(MarkerAssignment m) =>
        new(m.Id, m.SubjectId, m.TeacherId, m.AliasStart, m.AliasEnd, m.AssignedBy, m.AssignedAt);
}
