using ExamCatalogService.Application.DTOs;

namespace ExamCatalogService.Application.Interfaces;

public interface IScoreGridTemplateRepository
{
    Task<ScoreGridTemplateSummaryDto> CreateAsync(
        string name, Guid createdBy, IReadOnlyList<QuestionInputDto> questions, CancellationToken ct = default);

    Task<IReadOnlyList<ScoreGridTemplateSummaryDto>> ListAsync(CancellationToken ct = default);

    Task<ScoreGridTemplateDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns false when no template with that id exists.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
