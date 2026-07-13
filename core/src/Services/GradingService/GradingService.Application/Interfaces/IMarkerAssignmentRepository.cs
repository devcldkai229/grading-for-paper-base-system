namespace GradingService.Application.Interfaces;

/// <summary>Read model for GradingService.API's internal (service-to-service) AssignmentsController.</summary>
public interface IMarkerAssignmentRepository
{
    /// <summary>All alias ranges assigned to a lecturer for a subject (a lecturer may have several).</summary>
    Task<IReadOnlyList<AliasRangeDto>> GetAliasRangesAsync(
        Guid subjectId, Guid lecturerId, CancellationToken ct = default);

    /// <summary>Distinct subject ids a lecturer has any marker assignment for.</summary>
    Task<IReadOnlyList<Guid>> GetAssignedSubjectIdsAsync(
        Guid lecturerId, CancellationToken ct = default);
}

public sealed record AliasRangeDto(int AliasStart, int AliasEnd);
