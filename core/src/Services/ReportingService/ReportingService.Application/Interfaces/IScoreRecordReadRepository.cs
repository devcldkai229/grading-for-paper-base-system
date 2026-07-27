using ReportingService.Domain.Entities;

namespace ReportingService.Application.Interfaces;

/// <summary>Reads the local per-paper submitted-score projection (populated by event consumers).</summary>
public interface IScoreRecordReadRepository
{
    Task<IReadOnlyList<ScoreRecord>> GetBySubjectsAsync(
        IReadOnlyCollection<Guid> subjectIds, CancellationToken ct = default);
}
