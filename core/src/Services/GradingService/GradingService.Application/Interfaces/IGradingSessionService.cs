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
}
