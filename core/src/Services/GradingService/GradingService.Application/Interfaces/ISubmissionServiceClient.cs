using GradingService.Application.DTOs;

namespace GradingService.Application.Interfaces;

public interface ISubmissionServiceClient
{
    Task<BatchPapersClientDto?> GetBatchPapersAsync(Guid batchId, CancellationToken ct = default);

    Task<InternalPaperSummaryClientDto?> GetPaperSummaryAsync(Guid paperId, CancellationToken ct = default);
}
