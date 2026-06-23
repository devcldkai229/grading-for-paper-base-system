using ExamCatalogService.Application.DTOs;

namespace ExamCatalogService.Application.Interfaces;

public interface IAiGradingClient
{
    Task<ExtractGridResponse?> ExtractRubricAsync(
        string presignedUrl,
        string contentType,
        decimal? subjectMaxScore,
        CancellationToken ct = default);
}
