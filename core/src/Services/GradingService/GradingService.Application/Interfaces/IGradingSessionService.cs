using GradingService.Application.DTOs;

namespace GradingService.Application.Interfaces;

public interface IGradingSessionService
{
    Task<StartBatchResultDto?> StartBatchAsync(Guid batchId, Guid teacherId, CancellationToken ct = default);

    Task<GradingSessionDto?> GetSessionAsync(Guid assignmentId, Guid teacherId, CancellationToken ct = default);

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
}
