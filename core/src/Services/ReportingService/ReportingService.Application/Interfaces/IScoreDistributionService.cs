using ReportingService.Application.DTOs;

namespace ReportingService.Application.Interfaces;

public interface IScoreDistributionService
{
    /// <summary>
    /// Admin score distribution report: a 10-bucket histogram plus min/avg/max, per subject,
    /// optionally scoped to a semester and/or exam. Returns an error message instead of throwing
    /// when either upstream service (ExamCatalogService, GradingService) is unreachable.
    /// </summary>
    Task<(ScoreDistributionResultDto? Result, string? Error)> GetDistributionAsync(
        Guid? semesterId, Guid? examId, CancellationToken ct = default);
}
