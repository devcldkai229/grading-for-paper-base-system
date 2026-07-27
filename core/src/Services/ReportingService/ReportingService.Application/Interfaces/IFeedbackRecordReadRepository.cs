using ReportingService.Domain.Entities;

namespace ReportingService.Application.Interfaces;

/// <summary>Reads the local per-paper feedback projection (populated by event consumers).</summary>
public interface IFeedbackRecordReadRepository
{
    Task<IReadOnlyList<FeedbackRecord>> GetBySubjectAsync(Guid subjectId, CancellationToken ct = default);
}
