using ReportingService.Application.DTOs;

namespace ReportingService.Application.Interfaces;

public interface IGradingServiceClient
{
    /// <summary>
    /// Releasable feedback for every Submitted paper in a subject. Returns null if GradingService
    /// is unreachable — callers must fail closed (report generation fails rather than returns partial data).
    /// </summary>
    Task<SubjectFeedbackClientDto?> GetReleasableFeedbackAsync(Guid subjectId, CancellationToken ct = default);

    /// <summary>
    /// Grading progress (completion %, throughput, ETA) per subject and per lecturer, scoped to
    /// <paramref name="subjectIds"/> (null/empty = every subject with at least one assignment).
    /// Returns null if GradingService is unreachable — callers must fail closed.
    /// </summary>
    Task<GradingProgressDashboardClientDto?> GetProgressDashboardAsync(
        IReadOnlyCollection<Guid>? subjectIds, CancellationToken ct = default);

    /// <summary>
    /// Raw submitted scores + min/avg/max per subject, scoped to <paramref name="subjectIds"/>
    /// (null/empty = every subject with at least one submitted paper). Returns null if
    /// GradingService is unreachable — callers must fail closed.
    /// </summary>
    Task<ScoreDistributionDashboardClientDto?> GetScoreDistributionAsync(
        IReadOnlyCollection<Guid>? subjectIds, CancellationToken ct = default);
}
