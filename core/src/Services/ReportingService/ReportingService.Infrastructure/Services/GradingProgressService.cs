using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;

namespace ReportingService.Infrastructure.Services;

public class GradingProgressService : IGradingProgressService
{
    private readonly IExamCatalogServiceClient _catalogClient;
    private readonly IGradingServiceClient _gradingClient;

    public GradingProgressService(IExamCatalogServiceClient catalogClient, IGradingServiceClient gradingClient)
    {
        _catalogClient = catalogClient;
        _gradingClient = gradingClient;
    }

    public async Task<(GradingProgressDashboardResultDto? Result, string? Error)> GetDashboardAsync(
        Guid? semesterId, Guid? examId, CancellationToken ct = default)
    {
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
        var progress = await _gradingClient.GetProgressDashboardAsync(subjectIds, ct);
        if (progress is null)
        {
            return (null, "GradingService is unreachable. Try again later.");
        }

        var progressBySubject = progress.Subjects.ToDictionary(s => s.SubjectId);

        // Every subject the filter matched is included, even with zero progress — an admin
        // scoping to "this semester" needs to see subjects grading hasn't started on yet, too.
        var merged = subjects
            .Select(s =>
            {
                progressBySubject.TryGetValue(s.Id, out var p);
                return new GradingProgressSubjectDto(
                    s.Id,
                    s.SubjectCode,
                    s.Title,
                    s.ExamName,
                    s.SemesterCode,
                    p?.TotalPapers ?? 0,
                    p?.CompletedPapers ?? 0,
                    p?.DraftingPapers ?? 0,
                    p?.NotStartedPapers ?? 0,
                    p?.CompletionPercent ?? 0,
                    p?.ScoreAvg,
                    p?.ScoreMin,
                    p?.ScoreMax,
                    p?.ThroughputPerHour,
                    p?.EstimatedFinish,
                    p?.Lecturers ?? Array.Empty<LecturerProgressClientDto>());
            })
            .OrderBy(s => s.CompletionPercent)
            .ToList();

        return (new GradingProgressDashboardResultDto(merged), null);
    }
}
