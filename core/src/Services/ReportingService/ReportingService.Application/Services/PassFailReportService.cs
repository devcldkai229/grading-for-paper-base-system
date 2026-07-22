using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;

namespace ReportingService.Application.Services;

public class PassFailReportService : IPassFailReportService
{
    private readonly IExamCatalogServiceClient _catalogClient;
    private readonly IScoreRecordReadRepository _scoreRepository;

    public PassFailReportService(
        IExamCatalogServiceClient catalogClient, IScoreRecordReadRepository scoreRepository)
    {
        _catalogClient = catalogClient;
        _scoreRepository = scoreRepository;
    }

    public async Task<(PassFailReportResultDto? Result, string? Error)> GetReportAsync(
        Guid? semesterId, Guid? examId, CancellationToken ct = default)
    {
        // ExamCatalog /search stays REST — it resolves the filter + MaxScore/PassScore metadata.
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

        // Submitted scores come from the local score_record projection kept in sync by Grading events.
        var scoreRecords = await _scoreRepository.GetBySubjectsAsync(subjectIds, ct);
        var scoresBySubject = scoreRecords
            .GroupBy(r => r.SubjectId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.TotalScore).ToList());

        // Every subject the filter matched is included, even without a PassScore configured or
        // without any submitted papers yet — those just get null counts/rate instead of being
        // silently dropped, so the admin sees it needs configuring.
        var merged = subjects
            .Select(s =>
            {
                scoresBySubject.TryGetValue(s.Id, out var scores);
                scores ??= new List<decimal>();

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
                    scores.Count,
                    passCount,
                    failCount,
                    passRate);
            })
            .OrderBy(s => s.SubjectCode)
            .ToList();

        return (new PassFailReportResultDto(merged), null);
    }
}
