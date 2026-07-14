using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;

namespace ReportingService.Application.Services;

public class PassFailReportService : IPassFailReportService
{
    private readonly IExamCatalogServiceClient _catalogClient;
    private readonly IGradingServiceClient _gradingClient;

    public PassFailReportService(IExamCatalogServiceClient catalogClient, IGradingServiceClient gradingClient)
    {
        _catalogClient = catalogClient;
        _gradingClient = gradingClient;
    }

    public async Task<(PassFailReportResultDto? Result, string? Error)> GetReportAsync(
        Guid? semesterId, Guid? examId, CancellationToken ct = default)
    {
        var subjects = await _catalogClient.SearchSubjectsAsync(semesterId, examId, ct);
        if (subjects is null)
        {
            return (null, "ExamCatalogService is unreachable. Try again later.");
        }

        if (subjects.Count == 0)
        {
            return (new PassFailReportResultDto(Array.Empty<SubjectPassFailResultDto>()), null);
        }

        var subjectIds = subjects.Select(s => s.Id).ToList();
        var distribution = await _gradingClient.GetScoreDistributionAsync(subjectIds, ct);
        if (distribution is null)
        {
            return (null, "GradingService is unreachable. Try again later.");
        }

        var bySubject = distribution.Subjects.ToDictionary(s => s.SubjectId);

        // Every subject the filter matched is included, even without a PassScore configured or
        // without any submitted papers yet — those just get null counts/rate instead of being
        // silently dropped, so the admin sees it needs configuring.
        var merged = subjects
            .Select(s =>
            {
                bySubject.TryGetValue(s.Id, out var d);
                var scores = d?.Scores ?? Array.Empty<decimal>();

                int? passCount = null;
                int? failCount = null;
                decimal? passRate = null;

                if (s.PassScore.HasValue)
                {
                    passCount = scores.Count(score => score >= s.PassScore.Value);
                    failCount = scores.Count - passCount.Value;
                    passRate = scores.Count > 0
                        ? Math.Round(100m * passCount.Value / scores.Count, 1)
                        : null;
                }

                return new SubjectPassFailResultDto(
                    s.Id,
                    s.SubjectCode,
                    s.Title,
                    s.ExamName,
                    s.SemesterCode,
                    s.MaxScore,
                    s.PassScore,
                    d?.SubmittedCount ?? 0,
                    passCount,
                    failCount,
                    passRate);
            })
            .OrderBy(s => s.SubjectCode)
            .ToList();

        return (new PassFailReportResultDto(merged), null);
    }
}
