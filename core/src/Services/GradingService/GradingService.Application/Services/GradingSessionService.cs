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
    private readonly ILecturerMarkerCodeRepository? _markerCodes;

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
        ILecturerMarkerCodeRepository? markerCodes = null)
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
        _markerCodes = markerCodes;
    }

    // ---------------------------------------------------------------------------
    // Phase 5 — event publishing that drives ReportingService's local projections.
    // Every publish is staged on the MassTransit bus outbox and flushed atomically by the next
    // SaveChangesAsync, so these MUST be called BEFORE the save (N7 — no dual-write).
    // ---------------------------------------------------------------------------

    /// <summary>Adds an audit-trail row AND mirrors it onto the bus as an <see cref="AuditLogRecorded"/>
    /// so Reporting can serve the admin global audit-log viewer from its own database.</summary>
    private Task RecordAuditAsync(AuditLog log, CancellationToken ct)
    {
        _auditLogs.Add(log);
        return _messagePublisher.PublishAsync(new AuditLogRecorded(
            Guid.NewGuid(), "grading", log.UserId, log.Action, log.EntityType,
            log.EntityId, log.OldValue, log.NewValue, log.Reason, DateTime.UtcNow), ct);
    }

    private Task PublishStatusChangedAsync(
        GradingAssignment assignment, int? aliasNumber, DateTime occurredAt, CancellationToken ct) =>
        _messagePublisher.PublishAsync(new GradingAssignmentStatusChanged(
            Guid.NewGuid(), assignment.Id, assignment.SubjectId, assignment.TeacherId,
            assignment.StudentPaperId, aliasNumber, assignment.Status.ToString(), occurredAt), ct);

    /// <summary>Publishes the "fat" score event (submit or override) carrying both the aggregate score
    /// and full per-question feedback so Reporting builds its score AND feedback projections from one message.</summary>
    private Task PublishScoreAsync(
        bool isOverride, GradingAssignment assignment, int? aliasNumber, string? studentAlias,
        DateTime occurredAt, CancellationToken ct)
    {
        var form = assignment.GradingForm!;
        var questions = form.QuestionGradeDetails
            .OrderBy(q => q.OrderIndex)
            .Select(q => new ScoreQuestionPayload(q.QuestionNumber, q.Label, q.Score, q.MaxScore, q.QuestionComment))
            .ToList();
        var maxScore = questions.Sum(q => q.MaxScore);

        return isOverride
            ? _messagePublisher.PublishAsync(new ScoreOverridden(
                Guid.NewGuid(), assignment.Id, assignment.SubjectId, assignment.TeacherId,
                assignment.StudentPaperId, aliasNumber, studentAlias, form.TotalScore, maxScore,
                form.PaperComment, questions, occurredAt), ct)
            : _messagePublisher.PublishAsync(new ScoreSubmitted(
                Guid.NewGuid(), assignment.Id, assignment.SubjectId, assignment.TeacherId,
                assignment.StudentPaperId, aliasNumber, studentAlias, form.TotalScore, maxScore,
                form.PaperComment, questions, occurredAt), ct);
    }

    public async Task<(StartBatchResultDto? Result, string? Error)> StartBatchAsync(
        Guid batchId, Guid teacherId, CancellationToken ct = default)
    {
        var batch = await _submissionClient.GetBatchPapersAsync(batchId, ct);
        if (batch is null || batch.Papers.Count == 0) return (null, null);

        IReadOnlyList<BatchPaperClientDto> eligiblePapers = batch.Papers;
        if (batch.UploadedBy != teacherId)
        {
            var ranges = await _markerAssignments.GetAliasRangesAsync(
                batch.SubjectId, batchId, teacherId, ct);
            if (ranges.Count == 0)
            {
                return (null, "Forbidden: you are not assigned to grade papers in this batch.");
            }

            eligiblePapers = batch.Papers
                .Where(p => p.AliasNumber.HasValue && ranges.Any(r =>
                    p.AliasNumber.Value >= r.AliasStart && p.AliasNumber.Value <= r.AliasEnd))
                .ToList();
        }

        var grid = await _catalogClient.GetGradingGridAsync(batch.SubjectId, ct);
        if (grid is null) return (null, null);

        if (string.Equals(grid.Status, "Closed", StringComparison.OrdinalIgnoreCase))
        {
            return (null, "This subject is closed; new grading sessions can no longer be started.");
        }

        var summaries = new List<AssignmentSummaryDto>();

        foreach (var paper in eligiblePapers)
        {
            var assignment = await EnsureAssignmentAsync(
                paper.PaperId, batch.SubjectId, teacherId, grid, paper.AliasNumber, ct);

            summaries.Add(new AssignmentSummaryDto(
                assignment.Id,
                paper.PaperId,
                paper.AliasNumber,
                ToStatusLabel(assignment.Status)));
        }

        await _submissionClient.SetPapersAssignmentStatusAsync(
            eligiblePapers.Select(p => p.PaperId).ToList(), assigned: true, ct);

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
                q.OrderIndex,
                q.AiDrafted,
                ToAiReviewStatusLabel(q.AiReviewStatus)))
            .ToList();

        var aiSuggestions = DeserializeAiSuggestions(assignment.GradingForm.AiSuggestionsJson);

        return new GradingSessionDto(
            assignment.Id,
            assignment.StudentPaperId,
            assignment.SubjectId,
            paper.BatchId,
            grid.MaxScore,
            grid.RubricVersion,
            ToStatusLabel(assignment.Status),
            ToAiStatusLabel(assignment.AiStatus),
            assignment.GradingForm.RowVersion,
            assignment.GradingForm.PaperComment,
            assignment.GradingForm.InternalComment,
            paper?.StudentAlias,
            paper?.AliasNumber,
            questions,
            assignment.IsFlagged,
            aiSuggestions,
            assignment.GradingForm.AiPaperComment);
    }

    private static IReadOnlyList<AiSuggestionDto> DeserializeAiSuggestions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<AiSuggestionDto>();
        try
        {
            return JsonSerializer.Deserialize<List<AiSuggestionDto>>(json) ?? new List<AiSuggestionDto>();
        }
        catch (JsonException)
        {
            return Array.Empty<AiSuggestionDto>();
        }
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

            var prevScore = detail.Score;
            var prevComment = detail.QuestionComment;

            detail.Score = input.Score;
            detail.QuestionComment = input.QuestionComment;

            if (detail.AiDrafted)
            {
                var scoreChanged = prevScore != input.Score;
                var commentChanged = !string.Equals(prevComment, input.QuestionComment, StringComparison.Ordinal);
                if (scoreChanged || commentChanged)
                    detail.AiReviewStatus = AiReviewStatus.Modified;
            }
        }

        form.PaperComment = request.PaperComment;
        form.InternalComment = request.InternalComment;
        form.TotalScore = form.QuestionGradeDetails.Sum(q => q.Score);
        form.RowVersion++;
        var prevStatus = assignment.Status;
        assignment.Status = GradingProgressStatus.Drafting;

        await RecordAuditAsync(new AuditLog
        {
            UserId = teacherId,
            Action = "ScoreUpdated",
            EntityType = "GradingForm",
            EntityId = form.Id,
            OldValue = oldSnapshot,
            NewValue = SerializeForm(form)
        }, ct);

        if (prevStatus != GradingProgressStatus.Drafting)
            await PublishStatusChangedAsync(assignment, null, DateTime.UtcNow, ct);

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

    public async Task<bool> SetFlagAsync(
        Guid assignmentId, Guid teacherId, bool isFlagged, CancellationToken ct = default)
    {
        var assignment = await _assignments.GetWithFormAsync(assignmentId, teacherId, asNoTracking: false, ct);
        if (assignment is null) return false;

        assignment.IsFlagged = isFlagged;
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<GradingQueuePageDto> GetGradingQueueAsync(
        Guid teacherId, GradingProgressStatus? status, bool? flaggedOnly, string? aliasSearch,
        int page, int pageSize, CancellationToken ct = default)
    {
        var rows = await _assignments.ListQueueRowsAsync(teacherId, status, flaggedOnly, ct);
        if (rows.Count == 0)
        {
            return new GradingQueuePageDto(Array.Empty<GradingQueueRowDto>(), page, pageSize, 0, 0);
        }

        var paperIds = rows.Select(r => r.StudentPaperId).Distinct().ToList();
        var papers = await _submissionClient.GetPaperSummariesAsync(paperIds, ct);
        var paperById = (papers ?? Array.Empty<InternalPaperSummaryClientDto>())
            .ToDictionary(p => p.Id);

        var joined = rows.Select(r =>
        {
            paperById.TryGetValue(r.StudentPaperId, out var paper);
            return (Row: r, StudentAlias: paper?.StudentAlias, AliasNumber: paper?.AliasNumber);
        });

        if (!string.IsNullOrWhiteSpace(aliasSearch))
        {
            var keyword = aliasSearch.Trim();
            var isNumeric = int.TryParse(keyword, out var aliasNumberMatch);
            joined = joined.Where(j =>
                (j.StudentAlias?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (isNumeric && j.AliasNumber == aliasNumberMatch));
        }

        var ordered = joined
            .OrderBy(j => j.AliasNumber ?? int.MaxValue)
            .ThenBy(j => j.Row.CreatedAt)
            .ToList();

        var totalCount = ordered.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new GradingQueueRowDto(
                j.Row.AssignmentId,
                j.StudentAlias,
                j.AliasNumber,
                j.Row.SubjectId,
                ToStatusLabel(j.Row.Status),
                j.Row.IsFlagged,
                j.Row.TotalScore,
                j.Row.SubmittedAt))
            .ToList();

        return new GradingQueuePageDto(items, page, pageSize, totalCount, totalPages);
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

        await RecordAuditAsync(new AuditLog
        {
            UserId = actingUserId,
            Action = "ScoreOverridden",
            EntityType = "GradingForm",
            EntityId = form.Id,
            OldValue = oldSnapshot,
            NewValue = SerializeForm(form),
            Reason = request.Reason
        }, ct);

        // Refresh Reporting's score + feedback projections with the overridden marks (latest-wins by
        // OccurredAt). Alias/student-alias are best-effort display data pulled from Submission.
        var overridePaper = await _submissionClient.GetPaperSummaryAsync(assignment.StudentPaperId, ct);
        await PublishScoreAsync(
            isOverride: true, assignment, overridePaper?.AliasNumber, overridePaper?.StudentAlias,
            DateTime.UtcNow, ct);

        // Only notify when someone other than the original marker made the change — the marker
        // doesn't need to be told about their own edit. Publish before SaveChanges so the outbox
        // row commits atomically with the score change (N7).
        if (actingUserId != assignment.TeacherId)
        {
            await _messagePublisher.PublishAsync(new RegradeNotificationEvent(
                Guid.NewGuid(), assignment.TeacherId, assignment.Id, assignment.SubjectId,
                request.Reason, DateTime.UtcNow), ct);
        }

        await _uow.SaveChangesAsync(ct);

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
        var assignment = await _assignments.GetWithFormAndDetailsAsync(assignmentId, teacherId, asNoTracking: false, ct);

        if (assignment is null) return (null, false, true);
        if (assignment.Status == GradingProgressStatus.Submitted) return (null, true, false);
        if (assignment.GradingForm is null) return (null, false, true);

        var paper = await _submissionClient.GetPaperSummaryAsync(assignment.StudentPaperId, ct);
        if (paper is null) return (null, false, true);

        var now = DateTime.UtcNow;
        assignment.Status = GradingProgressStatus.Submitted;
        assignment.GradingForm.SubmittedAt = now;

        foreach (var detail in assignment.GradingForm.QuestionGradeDetails)
        {
            if (detail.AiDrafted && detail.AiReviewStatus == AiReviewStatus.Pending)
                detail.AiReviewStatus = AiReviewStatus.Accepted;
        }

        await RecordAuditAsync(new AuditLog
        {
            UserId = teacherId,
            Action = "FormSubmitted",
            EntityType = "GradingForm",
            EntityId = assignment.GradingForm.Id,
            NewValue = JsonSerializer.Serialize(new { assignment.GradingForm.TotalScore })
        }, ct);

        // Drive Reporting's score/feedback projection + progress projection from this finalization.
        await PublishScoreAsync(
            isOverride: false, assignment, paper.AliasNumber, paper.StudentAlias, now, ct);
        await PublishStatusChangedAsync(assignment, paper.AliasNumber, now, ct);

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
        SubjectGradingGridClientDto grid, int? aliasNumber, CancellationToken ct)
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
        // Seed Reporting's progress projection with the new (NotStarted) paper before the save flushes the outbox.
        await PublishStatusChangedAsync(assignment, aliasNumber, DateTime.UtcNow, ct);
        await _uow.SaveChangesAsync(ct);

        return assignment;
    }

    private async Task<(int Count, string? Error)> MaterializeAssignmentsForRangeAsync(
        Guid subjectId, Guid batchId, Guid teacherId, int aliasStart, int aliasEnd,
        CancellationToken ct)
    {
        var batch = await _submissionClient.GetBatchPapersAsync(batchId, ct);
        if (batch is null) return (0, "Submission batch was not found or is currently unavailable.");
        if (batch.SubjectId != subjectId) return (0, "The selected batch does not belong to this subject.");

        var papers = batch.Papers
            .Where(p => p.AliasNumber.HasValue
                && p.AliasNumber.Value >= aliasStart
                && p.AliasNumber.Value <= aliasEnd)
            .OrderBy(p => p.AliasNumber)
            .ToList();
        if (papers.Count == 0) return (0, "No papers exist in the selected alias range for this batch.");

        var grid = await _catalogClient.GetGradingGridAsync(subjectId, ct);
        if (grid is null) return (0, "The grading grid for this subject is unavailable.");
        if (string.Equals(grid.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            return (0, "This subject is closed; new grading sessions can no longer be started.");

        foreach (var paper in papers)
        {
            await EnsureAssignmentAsync(
                paper.PaperId, subjectId, teacherId, grid, paper.AliasNumber, ct);
        }

        if (!await _submissionClient.SetPapersAssignmentStatusAsync(
                papers.Select(p => p.PaperId).ToList(), assigned: true, ct))
        {
            return (papers.Count, "Assignments were created, but paper statuses could not be synchronized.");
        }

        return (papers.Count, null);
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

    private static string ToAiStatusLabel(AiSyncStatus status) =>
        status switch
        {
            AiSyncStatus.Queued => "Queued",
            AiSyncStatus.Processing => "Processing",
            AiSyncStatus.Completed => "Completed",
            AiSyncStatus.Failed => "Failed",
            _ => "NotRequested"
        };

    private static string ToAiReviewStatusLabel(AiReviewStatus status) =>
        status switch
        {
            AiReviewStatus.Accepted => "Accepted",
            AiReviewStatus.Rejected => "Rejected",
            AiReviewStatus.Modified => "Modified",
            _ => "Pending"
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

        // Fetch lecturer marker codes from the local projection (replicated from IamService — N5).
        Dictionary<Guid, string> teacherMarkerCodes = new();
        if (_markerCodes != null)
        {
            teacherMarkerCodes = await _markerCodes.GetAllMarkerCodesAsync(ct);
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

        // Flush the bus outbox (this method otherwise only reads) so the event is actually delivered.
        await _uow.SaveChangesAsync(ct);

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
            var effectiveDeadline = info?.GradingDeadline ?? info?.ExamEndDate;
            return effectiveDeadline is null
                ? null
                : new UpcomingDeadlineDto(
                    s.SubjectId,
                    info!.SubjectCode,
                    info.ExamName,
                    effectiveDeadline.Value,
                    s.Total,
                    s.Submitted,
                    s.Total - s.Submitted,
                    s.NextAssignmentId);
        });

        var upcomingDeadlines = (await Task.WhenAll(deadlineTasks))
            .Where(d => d is not null)
            .Select(d => d!)
            .OrderBy(d => d.Deadline)
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
                    ComputeAvgGradingMinutesPerPaper(subjectGroup),
                    subjectGroup.Count(r => r.IsFlagged));
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
            ComputeAvgGradingMinutesPerPaper(rows),
            rows.Count(r => r.IsFlagged));
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
        return assignments.Select(a => ToDto(a)).ToList();
    }

    public async Task<(MarkerAssignmentDto? Result, string? Error)> CreateMarkerAssignmentAsync(
        Guid subjectId, CreateMarkerAssignmentRequest request, Guid assignedBy, CancellationToken ct = default)
    {
        if (request.BatchId == Guid.Empty)
            return (null, "BatchId is required because aliases are scoped to a submission batch.");

        var batch = await _submissionClient.GetBatchPapersAsync(request.BatchId, ct);
        if (batch is null) return (null, "Submission batch was not found or is currently unavailable.");
        if (batch.SubjectId != subjectId) return (null, "The selected batch does not belong to this subject.");
        var gradingGrid = await _catalogClient.GetGradingGridAsync(subjectId, ct);
        if (gradingGrid is null) return (null, "The grading grid for this subject is unavailable.");
        if (string.Equals(gradingGrid.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            return (null, "This subject is closed; new grading sessions can no longer be started.");

        var existing = await _markerAssignments.ListForBatchAsync(request.BatchId, ct);

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

        var maxAlias = batch.Papers.Where(p => p.AliasNumber.HasValue).Select(p => p.AliasNumber!.Value).DefaultIfEmpty(0).Max();
        if (maxAlias == 0) return (null, "This batch has no submitted papers yet; there is nothing to allocate.");
        if (aliasEnd > maxAlias) return (null, $"Alias end {aliasEnd} exceeds the highest alias number ({maxAlias}) for this batch.");

        var assignment = new MarkerAssignment
        {
            SubjectId = subjectId,
            BatchId = request.BatchId,
            TeacherId = request.TeacherId,
            AliasStart = aliasStart,
            AliasEnd = aliasEnd,
            AssignedBy = assignedBy
        };

        _markerAssignments.Add(assignment);

        // Publish before SaveChanges so both events land in the outbox within the same transaction.
        await PublishMarkerAssignmentChangedAsync(assignment, MarkerAssignmentChangeType.Created, ct);
        await PublishAssignmentNotificationAsync(assignment, ct);

        await _uow.SaveChangesAsync(ct);

        var materialized = await MaterializeAssignmentsForRangeAsync(
            subjectId, request.BatchId, request.TeacherId, aliasStart, aliasEnd, ct);
        if (materialized.Error is not null && materialized.Count == 0)
            return (null, materialized.Error);

        var warnings = materialized.Error is null ? Array.Empty<string>() : new[] { materialized.Error };
        return (ToDto(assignment, materialized.Count, warnings: warnings), null);
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

        var targetBatchId = request.BatchId == Guid.Empty ? assignment.BatchId : request.BatchId;
        var targetBatch = await _submissionClient.GetBatchPapersAsync(targetBatchId, ct);
        if (targetBatch is null) return (null, false, "Submission batch was not found or is currently unavailable.");
        if (targetBatch.SubjectId != assignment.SubjectId) return (null, false, "The selected batch does not belong to this subject.");
        var gradingGrid = await _catalogClient.GetGradingGridAsync(assignment.SubjectId, ct);
        if (gradingGrid is null) return (null, false, "The grading grid for this subject is unavailable.");
        if (string.Equals(gradingGrid.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            return (null, false, "This subject is closed; marker assignments cannot be changed.");

        var existing = await _markerAssignments.ListForBatchAsync(targetBatchId, ct);
        var overlaps = existing.Any(m =>
            m.Id != id && request.AliasStart <= m.AliasEnd && m.AliasStart <= request.AliasEnd);
        if (overlaps)
        {
            return (null, false, $"Alias range {request.AliasStart}-{request.AliasEnd} overlaps an existing assignment for this subject.");
        }

        var maxAlias = targetBatch.Papers.Where(p => p.AliasNumber.HasValue).Select(p => p.AliasNumber!.Value).DefaultIfEmpty(0).Max();
        if (request.AliasEnd > maxAlias)
            return (null, false, $"Alias end {request.AliasEnd} exceeds the highest alias number ({maxAlias}) for this batch.");

        var oldBatch = await _submissionClient.GetBatchPapersAsync(assignment.BatchId, ct);
        if (oldBatch is null) return (null, false, "The original submission batch is unavailable.");
        var oldPaperIds = oldBatch.Papers
            .Where(p => p.AliasNumber is >= 1 && p.AliasNumber >= assignment.AliasStart && p.AliasNumber <= assignment.AliasEnd)
            .Select(p => p.PaperId).ToList();
        var oldAssignments = await _assignments.ListTrackedByTeacherAndPaperIdsAsync(
            assignment.TeacherId, oldPaperIds, ct);
        if (oldAssignments.Any(a => a.Status != GradingProgressStatus.NotStarted))
            return (null, false, "Cannot reassign because one or more papers are already drafting or submitted.");

        _assignments.RemoveRange(oldAssignments);

        assignment.TeacherId = request.TeacherId;
        assignment.BatchId = targetBatchId;
        assignment.AliasStart = request.AliasStart;
        assignment.AliasEnd = request.AliasEnd;
        assignment.AssignedBy = assignedBy;
        assignment.AssignedAt = DateTime.UtcNow;

        await PublishMarkerAssignmentChangedAsync(assignment, MarkerAssignmentChangeType.Updated, ct);
        await PublishAssignmentNotificationAsync(assignment, ct);

        await _uow.SaveChangesAsync(ct);

        await _submissionClient.SetPapersAssignmentStatusAsync(oldPaperIds, assigned: false, ct);
        var materialized = await MaterializeAssignmentsForRangeAsync(
            assignment.SubjectId, targetBatchId, request.TeacherId,
            request.AliasStart, request.AliasEnd, ct);
        if (materialized.Error is not null && materialized.Count == 0)
            return (null, false, materialized.Error);
        var warnings = materialized.Error is null ? Array.Empty<string>() : new[] { materialized.Error };
        return (ToDto(assignment, materialized.Count, warnings: warnings), false, null);
    }

    /// <summary>Notifies the (possibly new) teacher that they now own this alias range.</summary>
    private Task PublishAssignmentNotificationAsync(MarkerAssignment assignment, CancellationToken ct) =>
        _messagePublisher.PublishAsync(new AssignmentNotificationEvent(
            Guid.NewGuid(), assignment.TeacherId, assignment.SubjectId,
            assignment.AliasStart, assignment.AliasEnd, DateTime.UtcNow), ct);

    /// <summary>
    /// Replicates the allocation change to any service that keeps a local authz projection
    /// (ExamCatalog, Submission, Insights) — cuts the synchronous fail-closed cross-calls (N5).
    /// </summary>
    private Task PublishMarkerAssignmentChangedAsync(
        MarkerAssignment assignment, MarkerAssignmentChangeType changeType, CancellationToken ct) =>
        _messagePublisher.PublishAsync(new MarkerAssignmentChanged(
            Guid.NewGuid(), assignment.Id, assignment.SubjectId, assignment.BatchId, assignment.TeacherId,
            assignment.AliasStart, assignment.AliasEnd, changeType, null, DateTime.UtcNow), ct);

    public async Task<(bool Deleted, string? Error)> DeleteMarkerAssignmentAsync(Guid id, CancellationToken ct = default)
    {
        var assignment = await _markerAssignments.GetByIdAsync(id, ct);
        if (assignment is null) return (false, null);

        var batch = await _submissionClient.GetBatchPapersAsync(assignment.BatchId, ct);
        if (batch is null) return (false, "The submission batch is unavailable.");
        var paperIds = batch.Papers
            .Where(p => p.AliasNumber.HasValue && p.AliasNumber.Value >= assignment.AliasStart && p.AliasNumber.Value <= assignment.AliasEnd)
            .Select(p => p.PaperId).ToList();
        var gradingAssignments = await _assignments.ListTrackedByTeacherAndPaperIdsAsync(
            assignment.TeacherId, paperIds, ct);
        if (gradingAssignments.Any(a => a.Status != GradingProgressStatus.NotStarted))
            return (false, "Cannot delete because one or more papers are already drafting or submitted.");

        _assignments.RemoveRange(gradingAssignments);
        _markerAssignments.Remove(assignment);

        await PublishMarkerAssignmentChangedAsync(assignment, MarkerAssignmentChangeType.Deleted, ct);

        await _uow.SaveChangesAsync(ct);
        await _submissionClient.SetPapersAssignmentStatusAsync(paperIds, assigned: false, ct);

        return (true, null);
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
        var cutoff = today.AddDays(reminderWindowDays);

        var subjectIds = pendingGroups.Select(g => g.SubjectId).Distinct().ToList();
        var examInfoResults = await Task.WhenAll(subjectIds.Select(async id =>
            new { SubjectId = id, Info = await _catalogClient.GetExamInfoAsync(id, ct) }));
        var deadlineBySubject = examInfoResults
            .Select(x => new { x.SubjectId, x.Info, Effective = x.Info?.GradingDeadline ?? x.Info?.ExamEndDate })
            .Where(x => x.Effective is not null)
            .ToDictionary(x => x.SubjectId, x => (Info: x.Info!, Deadline: x.Effective!.Value));

        var publishedCount = 0;
        foreach (var group in pendingGroups)
        {
            if (!deadlineBySubject.TryGetValue(group.SubjectId, out var entry)) continue;
            if (entry.Deadline > cutoff) continue;

            var remaining = group.Total - group.Submitted;
            var messageId = DeterministicDailyMessageId(group.SubjectId, group.TeacherId, today);

            await _messagePublisher.PublishAsync(new DeadlineReminderEvent(
                messageId, group.TeacherId, group.SubjectId, entry.Info.SubjectCode,
                entry.Deadline, remaining, DateTime.UtcNow), ct);

            publishedCount++;
        }

        // Flush the bus outbox (this sweep otherwise only reads) so reminders are actually delivered.
        if (publishedCount > 0)
        {
            await _uow.SaveChangesAsync(ct);
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

    public async Task<(bool Success, string? Error)> ApplyAiSuggestionsAsync(
        ApplyAiSuggestionsRequest request, CancellationToken ct = default)
    {
        var assignment = await _assignments.GetWithFormAndDetailsAsync(
            request.AssignmentId, teacherId: null, asNoTracking: false, ct);

        if (assignment?.GradingForm is null)
            return (false, "Assignment not found");

        // Already submitted — skip gracefully (do not overwrite lecturer-finalized marks)
        if (assignment.Status == GradingProgressStatus.Submitted)
            return (true, null);

        var form = assignment.GradingForm;
        var validNumbers = form.QuestionGradeDetails
            .Select(q => q.QuestionNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Store AI results SEPARATELY from the manual marks. AI never mutates QuestionGradeDetails or
        // PaperComment — the lecturer copies suggestions into the manual marks explicitly from the AI
        // tab. We keep only suggestions that map to a real leaf on this form, clamped to its max.
        var maxByNumber = form.QuestionGradeDetails
            .ToDictionary(q => q.QuestionNumber, q => q.MaxScore, StringComparer.OrdinalIgnoreCase);

        var suggestions = request.QuestionGrades
            .Where(g => validNumbers.Contains(g.QuestionNumber))
            .Select(g => new AiSuggestionDto(
                g.QuestionNumber,
                Math.Clamp(g.Score, 0m, maxByNumber[g.QuestionNumber]),
                g.QuestionComment,
                g.Confidence,
                g.IsManualOnly))
            .ToList();

        form.AiSuggestionsJson = JsonSerializer.Serialize(suggestions);
        form.AiPaperComment = request.PaperComment;

        assignment.AiStatus = AiSyncStatus.Completed;

        await RecordAuditAsync(new AuditLog
        {
            UserId = Guid.Empty,
            Action = "AiSuggestionApplied",
            EntityType = "GradingAssignment",
            EntityId = assignment.Id,
            NewValue = JsonSerializer.Serialize(new
            {
                request.ModelUsed,
                request.PromptVersion,
                SuggestionCount = suggestions.Count
            })
        }, ct);

        await _uow.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool Accepted, string? Error, bool Conflict)> RequestAiSuggestionsAsync(
        Guid assignmentId, Guid teacherId, CancellationToken ct = default)
    {
        var assignment = await _assignments.GetWithFormAndDetailsAsync(
            assignmentId, teacherId, asNoTracking: false, ct);

        if (assignment?.GradingForm is null)
            return (false, "Session not found or access denied", false);

        if (assignment.Status == GradingProgressStatus.Submitted)
            return (false, "Session is already submitted", false);

        if (assignment.AiStatus is AiSyncStatus.Queued or AiSyncStatus.Processing)
            return (false, "AI grading is already in progress for this assignment", true);

        var files = await _submissionClient.GetPaperFileUrlsAsync(assignment.StudentPaperId, ct);
        if (files is null || files.Count == 0)
            return (false, "Paper files are unavailable for AI grading", false);

        var scoreGrid = assignment.GradingForm.QuestionGradeDetails
            .OrderBy(q => q.OrderIndex)
            .ThenBy(q => q.QuestionNumber)
            .Select(q => new AiGradeScoreGridItem(q.QuestionNumber, q.GroupLabel, q.Label, q.MaxScore))
            .ToList();

        if (scoreGrid.Count == 0)
            return (false, "Score grid is empty", false);

        var grid = await _catalogClient.GetGradingGridAsync(assignment.SubjectId, ct);
        var rubricVersion = grid?.RubricVersion.ToString() ?? "1";
        var rubricText = await _catalogClient.GetRubricTextAsync(assignment.SubjectId, ct);

        // Send the real barem file(s) so the AI grades against the actual questions/keys/guide,
        // not just the one-line-per-question score-grid summary. Best-effort: null when unreachable.
        var rubricSourceFiles = await _catalogClient.GetRubricSourceFilesAsync(assignment.SubjectId, ct);
        var rubricFiles = rubricSourceFiles?
            .Select(f => new AiGradeFileRefPayload(f.Url, f.ContentType))
            .ToList();

        // Prefer the APPROVED compiled contract (check-items + partial-credit) when available — this
        // supersedes the raw-file text for grading (Phase 3). Null → fall back to raw-file path.
        var compiledRubric = await _catalogClient.GetCompiledRubricAsync(assignment.SubjectId, null, ct);
        var enrichment = BuildContractEnrichment(compiledRubric);

        var payload = new AiGradeSuggestPayload(
            assignment.Id,
            assignment.SubjectId,
            rubricVersion,
            files.Select(f => new AiGradeFileRefPayload(f.Url, f.ContentType)).ToList(),
            scoreGrid.Select(q => EnrichScoreGridItem(q, enrichment)).ToList(),
            rubricText,
            rubricFiles,
            compiledRubric?.RubricVersion.ToString(),
            "vi",
            null);

        assignment.AiStatus = AiSyncStatus.Queued;

        // Publish first (collected by the MassTransit bus outbox), then SaveChanges flushes the
        // outbox row in the SAME transaction as the status change (N7 — no dual-write).
        var messageId = Guid.NewGuid();
        await _messagePublisher.PublishAsync(
            new AiGradeRequestedEvent(messageId, assignment.Id, teacherId, payload, DateTime.UtcNow),
            ct);

        await _uow.SaveChangesAsync(ct);

        return (true, null, false);
    }

    public async Task<bool> MarkAiGradeFailedAsync(Guid assignmentId, string reason, CancellationToken ct = default)
    {
        var assignment = await _assignments.GetWithFormAsync(
            assignmentId, teacherId: null, asNoTracking: false, ct);

        if (assignment is null) return false;

        // Never override a lecturer-finalized paper, and stay idempotent on repeated delivery.
        if (assignment.Status == GradingProgressStatus.Submitted) return true;
        if (assignment.AiStatus == AiSyncStatus.Failed) return true;

        assignment.AiStatus = AiSyncStatus.Failed;

        await RecordAuditAsync(new AuditLog
        {
            UserId = Guid.Empty,
            Action = "AiSuggestionFailed",
            EntityType = "GradingAssignment",
            EntityId = assignment.Id,
            NewValue = reason
        }, ct);

        await _uow.SaveChangesAsync(ct);
        return true;
    }

    private static MarkerAssignmentDto ToDto(
        MarkerAssignment m, int materializedCount = 0, int skippedInProgressCount = 0,
        IReadOnlyList<string>? warnings = null) =>
        new(m.Id, m.SubjectId, m.BatchId, m.TeacherId, m.AliasStart, m.AliasEnd,
            m.AssignedBy, m.AssignedAt, materializedCount, skippedInProgressCount, warnings);

    // ── Compiled-contract enrichment of the AI grade payload (Phase 3a) ──

    private sealed record ContractEnrichment(
        IReadOnlyDictionary<string, JsonElement> CriteriaByNumber,
        IReadOnlyDictionary<string, string> AssetUrlById);

    /// <summary>Index the approved contract JSON by question number + assets by id (empty when none).</summary>
    private static ContractEnrichment? BuildContractEnrichment(CompiledRubricClientDto? contract)
    {
        if (contract is null || string.IsNullOrWhiteSpace(contract.ContractJson))
        {
            return null;
        }

        var byNumber = new Dictionary<string, JsonElement>();
        try
        {
            using var doc = JsonDocument.Parse(contract.ContractJson);
            if (doc.RootElement.TryGetProperty("criteria", out var criteria)
                && criteria.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in criteria.EnumerateArray())
                {
                    if (c.TryGetProperty("questionNumber", out var qn) && qn.GetString() is { } key)
                    {
                        // Clone so the element stays valid after the JsonDocument is disposed.
                        byNumber[key] = c.Clone();
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        var assetUrls = contract.Assets.ToDictionary(a => a.AssetId, a => a.Url);
        return new ContractEnrichment(byNumber, assetUrls);
    }

    private static AiGradeScoreGridItemPayload EnrichScoreGridItem(
        AiGradeScoreGridItem q, ContractEnrichment? enrichment)
    {
        if (enrichment is null || !enrichment.CriteriaByNumber.TryGetValue(q.QuestionNumber, out var c))
        {
            return new AiGradeScoreGridItemPayload(q.QuestionNumber, q.GroupLabel, q.Label, q.MaxScore);
        }

        var checkItems = ReadArray(c, "checkItems", el => new AiGradeCheckItemPayload(
            GetString(el, "checkId") ?? "",
            GetString(el, "description") ?? "",
            GetDecimal(el, "points"),
            GetBool(el, "required"),
            GetString(el, "evidenceHint")));

        var partialCredit = ReadArray(c, "partialCredit", el => new AiGradePartialCreditPayload(
            GetString(el, "label") ?? "",
            GetDecimal(el, "score"),
            GetString(el, "condition") ?? "",
            ReadStringArray(el, "checkIds")));

        var visualAssetUrls = ReadStringArray(c, "visualAssetIds")
            .Select(id => enrichment.AssetUrlById.TryGetValue(id, out var url) ? url : null)
            .Where(url => url is not null)
            .Select(url => url!)
            .ToList();

        return new AiGradeScoreGridItemPayload(
            q.QuestionNumber,
            q.GroupLabel,
            q.Label,
            q.MaxScore,
            GetString(c, "parentQuestion"),
            GetString(c, "questionText"),
            GetString(c, "answerKey"),
            checkItems,
            partialCredit,
            ReadStringArray(c, "commonMistakes"),
            ReadStringArray(c, "notPenalize"),
            GetBool(c, "requiresVisual"),
            visualAssetUrls,
            GetString(c, "specificity"),
            GetString(c, "extractionConfidence"));
    }

    private static IReadOnlyList<T> ReadArray<T>(JsonElement parent, string prop, Func<JsonElement, T> map)
    {
        if (!parent.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<T>();
        }
        return arr.EnumerateArray().Select(map).ToList();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement parent, string prop)
    {
        if (!parent.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }
        return arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()!)
            .ToList();
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal GetDecimal(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0m;

    private static bool GetBool(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v)
        && v.ValueKind is JsonValueKind.True or JsonValueKind.False && v.GetBoolean();
}
