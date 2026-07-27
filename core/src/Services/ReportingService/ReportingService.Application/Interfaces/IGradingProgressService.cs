using ReportingService.Application.DTOs;

namespace ReportingService.Application.Interfaces;

public interface IGradingProgressService
{
    /// <summary>
    /// Admin grading progress dashboard: completion %, throughput and ETA per subject and per
    /// lecturer, optionally scoped to a semester and/or exam. Returns an error message instead of
    /// throwing when either upstream service (ExamCatalogService, GradingService) is unreachable.
    /// </summary>
    Task<(GradingProgressDashboardResultDto? Result, string? Error)> GetDashboardAsync(
        Guid? semesterId, Guid? examId, CancellationToken ct = default);
}
