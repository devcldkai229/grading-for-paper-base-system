using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using ExamCatalogService.Domain.Enums;

namespace ExamCatalogService.Application.Services;

public class SubjectQueryService : ISubjectQueryService
{
    private readonly IExamCatalogRepository _repository;

    public SubjectQueryService(IExamCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<SubjectSearchResult> SearchSubjectsAsync(
        string? code,
        Guid? semesterId,
        Guid? examId,
        string? status,
        Guid? lecturerId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        SubjectStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<SubjectStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                return new SubjectSearchResult(
                    Array.Empty<SubjectSearchResultDto>(), 0, page, pageSize,
                    $"Invalid status '{status}'. Allowed: Draft, Open, Grading, Closed.");
            }
            statusFilter = parsedStatus;
        }

        IReadOnlySet<Guid>? restrictToSubjectIds = null;
        if (lecturerId.HasValue)
        {
            // Read from the local marker_assignment_view projection (kept in sync via
            // MarkerAssignmentChanged events) — no synchronous cross-call to GradingService.
            var assignedSubjectIds = await _repository.GetAssignedSubjectIdsAsync(lecturerId.Value, ct);
            restrictToSubjectIds = assignedSubjectIds.ToHashSet();
        }

        var (items, totalCount) = await _repository.SearchSubjectsAsync(
            code, semesterId, examId, statusFilter, restrictToSubjectIds, page, pageSize, ct);

        return new SubjectSearchResult(items, totalCount, page, pageSize, null);
    }
}
