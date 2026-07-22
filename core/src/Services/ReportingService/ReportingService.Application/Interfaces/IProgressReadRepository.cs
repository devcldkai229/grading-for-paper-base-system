using ReportingService.Domain.Entities;

namespace ReportingService.Application.Interfaces;

/// <summary>Reads the local grading-progress projections (populated by event consumers).</summary>
public interface IProgressReadRepository
{
    Task<IReadOnlyList<SubjectProgress>> GetSubjectProgressAsync(
        IReadOnlyCollection<Guid> subjectIds, CancellationToken ct = default);

    Task<IReadOnlyList<MarkerProgress>> GetMarkerProgressAsync(
        IReadOnlyCollection<Guid> subjectIds, CancellationToken ct = default);
}
