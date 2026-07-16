using ExamCatalogService.Application.DTOs;

namespace ExamCatalogService.Application.Interfaces;

public interface ISubjectQueryService
{
    /// <summary>
    /// Cross-exam subject search by code, semester, exam and/or status (paginated).
    /// Pass <paramref name="lecturerId"/> to scope results to subjects that lecturer has a marker
    /// assignment for (fetched from GradingService — fails closed to an empty result if unreachable);
    /// pass null for an admin-scoped search (sees everything matching the filter).
    /// </summary>
    Task<SubjectSearchResult> SearchSubjectsAsync(
        string? code,
        Guid? semesterId,
        Guid? examId,
        string? status,
        Guid? lecturerId,
        int page,
        int pageSize,
        CancellationToken ct = default);
}

/// <summary>
/// Page/PageSize echo back the clamped values actually used for the query. Error is set (Items/TotalCount
/// default) when <c>status</c> failed to parse — callers should treat that as a 400.
/// </summary>
public sealed record SubjectSearchResult(
    IReadOnlyList<SubjectSearchResultDto> Items, int TotalCount, int Page, int PageSize, string? Error);
