using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;

namespace ReportingService.Application.Services;

public class GradingProgressService : IGradingProgressService
{
    private readonly IExamCatalogServiceClient _catalogClient;
    private readonly IProgressReadRepository _progressRepository;

    public GradingProgressService(
        IExamCatalogServiceClient catalogClient, IProgressReadRepository progressRepository)
    {
        _catalogClient = catalogClient;
        _progressRepository = progressRepository;
    }

    public async Task<(GradingProgressDashboardResultDto? Result, string? Error)> GetDashboardAsync(
        Guid? semesterId, Guid? examId, CancellationToken ct = default)
    {
        // ExamCatalog /search stays REST — it resolves the semester/exam filter into a subject list.
        var subjects = await _catalogClient.SearchSubjectsAsync(semesterId, examId, ct);
        if (subjects is null)
        {
            return (null, "ExamCatalogService is unreachable. Try again later.");
        }

        if (subjects.Count == 0)
        {
            return (new GradingProgressDashboardResultDto(Array.Empty<GradingProgressSubjectDto>()), null);
        }

        var subjectIds = subjects.Select(s => s.Id).ToList();

        // Progress now comes from local projections kept up to date by Grading events.
        var subjectProgress = await _progressRepository.GetSubjectProgressAsync(subjectIds, ct);
        var markerProgress = await _progressRepository.GetMarkerProgressAsync(subjectIds, ct);

        var progressBySubject = subjectProgress.ToDictionary(s => s.SubjectId);
        var markersBySubject = markerProgress
            .GroupBy(m => m.SubjectId)
            .ToDictionary(g => g.Key, g => g
                .OrderBy(m => m.TeacherId)
                .Select(m => new LecturerProgressClientDto(
                    m.TeacherId,
                    m.AssignedCount,
                    m.CompletedCount,
                    m.DraftingCount,
                    Math.Max(0, m.AssignedCount - m.CompletedCount - m.DraftingCount),
                    m.AvgScore,
                    m.ThroughputPerHour,
                    m.LastActivityAt,
                    EstimatedFinish: null,
                    AvgGradingMinutesPerPaper: null,
                    FlaggedCount: 0))
                .ToList());

        // Every subject the filter matched is included, even with zero progress — an admin
        // scoping to "this semester" needs to see subjects grading hasn't started on yet, too.
        var merged = subjects
            .Select(s =>
            {
                progressBySubject.TryGetValue(s.Id, out var p);
                markersBySubject.TryGetValue(s.Id, out var lecturers);
                lecturers ??= new List<LecturerProgressClientDto>();

                var total = p?.TotalPapers ?? 0;
                var completed = p?.CompletedPapers ?? 0;
                var completionPercent = total > 0 ? Math.Round(100m * completed / total, 1) : 0m;

                // Subject-level throughput is aggregated from its markers (analytics that need heartbeat
                // timing — EstimatedFinish, AvgGradingMinutesPerPaper, FlaggedCount — aren't event-derived).
                decimal? throughput = lecturers.Any(l => l.ThroughputPerHour.HasValue)
                    ? lecturers.Where(l => l.ThroughputPerHour.HasValue).Sum(l => l.ThroughputPerHour!.Value)
                    : null;

                return new GradingProgressSubjectDto(
                    s.Id,
                    s.SubjectCode,
                    s.Title,
                    s.ExamName,
                    s.SemesterCode,
                    total,
                    completed,
                    p?.InProgress ?? 0,
                    p?.NotStarted ?? 0,
                    completionPercent,
                    p?.ScoreAvg,
                    p?.ScoreMin,
                    p?.ScoreMax,
                    throughput,
                    EstimatedFinish: null,
                    lecturers,
                    AvgGradingMinutesPerPaper: null,
                    s.GradingDeadline,
                    FlaggedCount: 0);
            })
            .OrderBy(s => s.CompletionPercent)
            .ToList();

        return (new GradingProgressDashboardResultDto(merged), null);
    }
}
