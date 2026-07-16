using ReportingService.Application.DTOs;

namespace ReportingService.Application.Interfaces;

public interface IPassFailReportService
{
    /// <summary>
    /// Admin pass/fail report: student counts and pass rate per subject, computed against each
    /// subject's own configured PassScore threshold. Optionally scoped to a semester and/or exam.
    /// Subjects without a PassScore configured yet are still included, with null counts/rate.
    /// Returns an error message instead of throwing when either upstream service
    /// (ExamCatalogService, GradingService) is unreachable.
    /// </summary>
    Task<(PassFailReportResultDto? Result, string? Error)> GetReportAsync(
        Guid? semesterId, Guid? examId, CancellationToken ct = default);
}
