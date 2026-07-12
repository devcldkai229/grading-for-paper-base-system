using ReportingService.Application.DTOs;

namespace ReportingService.Application.Interfaces;

public interface IGradingServiceClient
{
    /// <summary>
    /// Releasable feedback for every Submitted paper in a subject. Returns null if GradingService
    /// is unreachable — callers must fail closed (report generation fails rather than returns partial data).
    /// </summary>
    Task<SubjectFeedbackClientDto?> GetReleasableFeedbackAsync(Guid subjectId, CancellationToken ct = default);
}
