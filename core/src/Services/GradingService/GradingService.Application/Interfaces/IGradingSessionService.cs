using GradingService.Application.DTOs;

namespace GradingService.Application.Interfaces;

public interface IGradingSessionService
{
    Task<StartBatchResultDto?> StartBatchAsync(Guid batchId, Guid teacherId, CancellationToken ct = default);

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

    Task<byte[]?> ExportGradesAsync(Guid subjectId, CancellationToken ct = default);

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
}
