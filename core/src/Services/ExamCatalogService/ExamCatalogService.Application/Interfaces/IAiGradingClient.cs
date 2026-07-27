using ExamCatalogService.Application.DTOs;

namespace ExamCatalogService.Application.Interfaces;

public interface IAiGradingClient
{
    Task<ExtractGridResponse?> ExtractRubricAsync(
        string presignedUrl,
        string contentType,
        decimal? subjectMaxScore,
        CancellationToken ct = default);

    /// <summary>
    /// Compile a subject's barem into a grading contract (blocks + check-items + partial-credit +
    /// asset crops). Returns the raw JSON body of the AI ingestion response, or null if the AI
    /// service is unreachable / returns an error.
    /// </summary>
    Task<string?> IngestRubricAsync(IngestRubricRequestDto request, CancellationToken ct = default);
}
