using GradingService.Application.DTOs;

namespace GradingService.Application.Interfaces;

public interface IGradingSessionService
{
    /// <summary>
    /// Starts (or resumes) a grading session for every paper in the batch. Returns a null Result
    /// with a null Error for the pre-existing not-found/forbidden cases (batch missing, wrong
    /// uploader, subject grid missing) — the controller maps that to a generic 404. Returns a
    /// non-null Error (and null Result) when the subject's status is Closed, since closed subjects
    /// no longer allow new grading sessions to start.
    /// </summary>
    Task<(StartBatchResultDto? Result, string? Error)> StartBatchAsync(
        Guid batchId, Guid teacherId, CancellationToken ct = default);

    Task<GradingSessionDto?> GetSessionAsync(Guid assignmentId, Guid teacherId, CancellationToken ct = default);

    /// <summary>
    /// Admin-only lookup of any assignment (regardless of marker/status) for the override tool.
    /// Unlike GetSessionAsync, this does not scope by teacherId and does not touch the caller's
    /// own resume-queue pointer (the caller isn't necessarily the assignment's marker).
    /// </summary>
    Task<GradingSessionDto?> GetAssignmentForOverrideAsync(Guid assignmentId, CancellationToken ct = default);

    Task<(SaveMarksResultDto? Result, bool Conflict, string? Error)> SaveMarksAsync(
        Guid assignmentId, Guid teacherId, SaveMarksRequest request, CancellationToken ct = default);

    Task<(SubmitResultDto? Result, bool Forbidden, bool NotFound)> SubmitAsync(
        Guid assignmentId, Guid teacherId, CancellationToken ct = default);

    Task<byte[]?> ExportGradesAsync(Guid subjectId, Guid requestedBy, CancellationToken ct = default);

    /// <summary>
    /// Adds a heartbeat "tick" of active grading time (seconds, clamped) to the assignment's
    /// GradingForm, scoped to its own marker, only while grading hasn't been submitted yet.
    /// Feeds the AvgGradingMinutesPerPaper figure in the progress dashboard. A rare concurrency
    /// clash with a simultaneous SaveMarks/Submit call is swallowed — this is analytics-only data,
    /// losing one heartbeat tick occasionally is inconsequential. Returns false if the assignment
    /// doesn't exist, isn't owned by teacherId, or is already Submitted.
    /// </summary>
    Task<bool> RecordHeartbeatAsync(Guid assignmentId, Guid teacherId, int seconds, CancellationToken ct = default);

    /// <summary>
    /// Aggregate grading progress for a lecturer's dashboard: done/total counters, upcoming exam
    /// deadlines for subjects that still have ungraded papers (soonest first), and a quick-link
    /// assignment id to resume grading (the next ungraded paper for the most urgent deadline, or
    /// any remaining one if no subject has a known deadline).
    /// </summary>
    Task<MyProgressDto> GetMyProgressAsync(Guid teacherId, CancellationToken ct = default);

    /// <summary>
    /// Admin/Coordinator (isAdmin=true) may override any assignment's scores regardless of status.
    /// A Lecturer (isAdmin=false) may only override an assignment they themselves are the marker for.
    /// Unlike SaveMarksAsync, this bypasses the "already Submitted" lock — that's the point of an override.
    /// </summary>
    Task<(OverrideMarksResultDto? Result, bool NotFound, string? Error)> OverrideMarksAsync(
        Guid assignmentId, Guid actingUserId, bool isAdmin, OverrideMarksRequest request, CancellationToken ct = default);

    /// <summary>
    /// Full history of ScoreUpdated/FormSubmitted/ScoreOverridden entries for an assignment's grading form,
    /// newest first. Same ownership rule as OverrideMarksAsync (Admin: any; Lecturer: own only).
    /// </summary>
    Task<(IReadOnlyList<AuditLogEntryDto>? Result, bool NotFound)> GetAuditTrailAsync(
        Guid assignmentId, Guid actingUserId, bool isAdmin, CancellationToken ct = default);

    /// <summary>
    /// Releasable (public-facing) feedback for every Submitted paper in a subject — total score,
    /// paper comment and per-question scores/comments. Deliberately never reads GradingForm's
    /// InternalComment field, and skips assignments that aren't Submitted yet (not yet finalized/releasable).
    /// Consumed by ReportingService's student feedback export (service-to-service, internal API key).
    /// </summary>
    Task<SubjectFeedbackExportDto> GetReleasableFeedbackAsync(Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Admin grading progress dashboard: completion %, throughput (papers/hour, based on
    /// GradingForm.SubmittedAt in the last 24h) and a projected ETA, broken down per subject and
    /// per lecturer within each subject. Pass <paramref name="subjectIds"/> to scope to a specific
    /// set of subjects (e.g. resolved from a semester/exam filter); null/empty means "all subjects
    /// that have at least one grading assignment".
    /// </summary>
    Task<GradingProgressDashboardDto> GetProgressDashboardAsync(
        IReadOnlyCollection<Guid>? subjectIds, CancellationToken ct = default);

    /// <summary>
    /// Raw submitted scores + min/avg/max per subject, for the admin score distribution report.
    /// Pass <paramref name="subjectIds"/> to scope to a specific set of subjects (e.g. resolved
    /// from a semester/exam filter); null/empty means every subject with at least one submitted paper.
    /// </summary>
    Task<ScoreDistributionDashboardDto> GetScoreDistributionAsync(
        IReadOnlyCollection<Guid>? subjectIds, CancellationToken ct = default);

    /// <summary>
    /// Unscoped, filterable audit log listing (score overrides, submissions, etc. across every
    /// assignment) for ReportingService's global audit log viewer. Unlike GetAuditTrailAsync,
    /// this is not tied to one assignment and has no ownership check (internal, service-to-service only).
    /// </summary>
    Task<AuditLogPageDto> GetAuditLogsAsync(
        Guid? userId, string? entityType, string? action, int page, int pageSize, CancellationToken ct = default);

    /// <summary>All marker assignments (alias ranges per lecturer) for a subject, ordered by alias start.</summary>
    Task<IReadOnlyList<MarkerAssignmentDto>> ListMarkerAssignmentsAsync(
        Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Admin allocates an alias range to a lecturer for a subject, either explicitly
    /// (AliasStart/AliasEnd) or by Quota (auto-picks the next contiguous range after the highest
    /// AliasEnd already assigned for the subject). Rejects a range that overlaps any existing
    /// assignment for the same subject, regardless of lecturer.
    /// </summary>
    Task<(MarkerAssignmentDto? Result, string? Error)> CreateMarkerAssignmentAsync(
        Guid subjectId, CreateMarkerAssignmentRequest request, Guid assignedBy, CancellationToken ct = default);

    /// <summary>
    /// Admin reassigns an existing marker assignment: changes its lecturer and/or alias range.
    /// Rejects a range that overlaps any other assignment for the same subject.
    /// </summary>
    Task<(MarkerAssignmentDto? Result, bool NotFound, string? Error)> ReassignMarkerAssignmentAsync(
        Guid id, ReassignMarkerAssignmentRequest request, Guid assignedBy, CancellationToken ct = default);

    /// <summary>Admin removes a marker assignment (frees the range for reallocation).</summary>
    Task<bool> DeleteMarkerAssignmentAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Scans every (subject, teacher) pair with ungraded papers and publishes a
    /// DeadlineReminderEvent for those whose subject's exam deadline falls within
    /// <paramref name="reminderWindowDays"/>. Each event's MessageId is deterministic
    /// (derived from subject + teacher + calendar day), so re-running the sweep multiple times
    /// on the same day is a no-op on the consumer side (Redis dedup) rather than a duplicate
    /// notification — this is the job's idempotency guarantee. Returns the number of reminders
    /// published (for logging).
    /// </summary>
    Task<int> RunDeadlineReminderSweepAsync(int reminderWindowDays = 3, CancellationToken ct = default);
}
