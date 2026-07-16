using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;

namespace ReportingService.Application.Services;

public class ScoreDistributionService : IScoreDistributionService
{
    private const int BucketCount = 10;

    private readonly IExamCatalogServiceClient _catalogClient;
    private readonly IGradingServiceClient _gradingClient;

    public ScoreDistributionService(IExamCatalogServiceClient catalogClient, IGradingServiceClient gradingClient)
    {
        _catalogClient = catalogClient;
        _gradingClient = gradingClient;
    }

    public async Task<(ScoreDistributionResultDto? Result, string? Error)> GetDistributionAsync(
        Guid? semesterId, Guid? examId, CancellationToken ct = default)
    {
        var subjects = await _catalogClient.SearchSubjectsAsync(semesterId, examId, ct);
        if (subjects is null)
        {
            return (null, "ExamCatalogService is unreachable. Try again later.");
        }

        if (subjects.Count == 0)
        {
            return (new ScoreDistributionResultDto(Array.Empty<SubjectScoreDistributionResultDto>()), null);
        }

        var subjectIds = subjects.Select(s => s.Id).ToList();
        var distribution = await _gradingClient.GetScoreDistributionAsync(subjectIds, ct);
        if (distribution is null)
        {
            return (null, "GradingService is unreachable. Try again later.");
        }

        var bySubject = distribution.Subjects.ToDictionary(s => s.SubjectId);

        // Every subject the filter matched is included, even with no submitted papers yet —
        // it just gets an all-zero histogram instead of being silently dropped.
        var merged = subjects
            .Select(s =>
            {
                bySubject.TryGetValue(s.Id, out var d);
                var scores = d?.Scores ?? Array.Empty<decimal>();
                return new SubjectScoreDistributionResultDto(
                    s.Id,
                    s.SubjectCode,
                    s.Title,
                    s.ExamName,
                    s.SemesterCode,
                    s.MaxScore,
                    d?.SubmittedCount ?? 0,
                    d?.ScoreAvg,
                    d?.ScoreMin,
                    d?.ScoreMax,
                    BuildHistogram(scores, s.MaxScore));
            })
            .OrderBy(s => s.SubjectCode)
            .ToList();

        return (new ScoreDistributionResultDto(merged), null);
    }

    /// <summary>
    /// Splits [0, maxScore] into <see cref="BucketCount"/> equal-width buckets and counts how many
    /// scores fall in each. A score exactly equal to maxScore lands in the last bucket rather than
    /// overflowing into a non-existent 11th one.
    /// </summary>
    private static IReadOnlyList<ScoreHistogramBucketDto> BuildHistogram(
        IReadOnlyCollection<decimal> scores, decimal maxScore)
    {
        var buckets = new int[BucketCount];
        var bucketSize = maxScore / BucketCount;

        if (bucketSize > 0)
        {
            foreach (var score in scores)
            {
                var index = (int)(score / bucketSize);
                if (index >= BucketCount) index = BucketCount - 1;
                if (index < 0) index = 0;
                buckets[index]++;
            }
        }

        var result = new List<ScoreHistogramBucketDto>(BucketCount);
        for (var i = 0; i < BucketCount; i++)
        {
            result.Add(new ScoreHistogramBucketDto(
                Math.Round(bucketSize * i, 2),
                Math.Round(bucketSize * (i + 1), 2),
                buckets[i]));
        }

        return result;
    }
}
