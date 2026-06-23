using GradingService.Application.DTOs;

namespace GradingService.Application.Interfaces;

public interface IExamCatalogServiceClient
{
    Task<SubjectGradingGridClientDto?> GetGradingGridAsync(Guid subjectId, CancellationToken ct = default);
}
