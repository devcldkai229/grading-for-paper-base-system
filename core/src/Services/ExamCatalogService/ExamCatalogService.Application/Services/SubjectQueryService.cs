using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using ExamCatalogService.Domain.Enums;

namespace ExamCatalogService.Application.Services;

public class SubjectQueryService : ISubjectQueryService
{
    private readonly IExamCatalogRepository _repository;
    private readonly IGradingServiceClient _gradingServiceClient;

    public SubjectQueryService(IExamCatalogRepository repository, IGradingServiceClient gradingServiceClient)
    {
        _repository = repository;
        _gradingServiceClient = gradingServiceClient;
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
            var assignedSubjectIds = await _gradingServiceClient.GetAssignedSubjectIdsAsync(lecturerId.Value, ct);
            // Fail-closed: GradingService unreachable => show nothing rather than everything.
            restrictToSubjectIds = (assignedSubjectIds ?? Array.Empty<Guid>()).ToHashSet();
        }

        var (items, totalCount) = await _repository.SearchSubjectsAsync(
            code, semesterId, examId, statusFilter, restrictToSubjectIds, page, pageSize, ct);

        return new SubjectSearchResult(items, totalCount, page, pageSize, null);
    }
}
