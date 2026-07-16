using GradingService.Domain.Entities;

namespace GradingService.Application.Interfaces;

/// <summary>
/// Read model for GradingService.API's internal (service-to-service) AssignmentsController,
/// plus the write path backing the admin "distribute papers to graders" feature.
/// </summary>
public interface IMarkerAssignmentRepository
{
    /// <summary>All alias ranges assigned to a lecturer for a subject (a lecturer may have several).</summary>
    Task<IReadOnlyList<AliasRangeDto>> GetAliasRangesAsync(
        Guid subjectId, Guid lecturerId, CancellationToken ct = default);

    /// <summary>Distinct subject ids a lecturer has any marker assignment for.</summary>
    Task<IReadOnlyList<Guid>> GetAssignedSubjectIdsAsync(
        Guid lecturerId, CancellationToken ct = default);

    /// <summary>All marker assignments for a subject (every teacher), ordered by alias start. Untracked.</summary>
    Task<IReadOnlyList<MarkerAssignment>> ListForSubjectAsync(
        Guid subjectId, CancellationToken ct = default);

    /// <summary>Loads one marker assignment, tracked, for update/delete.</summary>
    Task<MarkerAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Stages a new marker assignment for insertion (caller must call IUnitOfWork.SaveChangesAsync).</summary>
    void Add(MarkerAssignment assignment);

    /// <summary>Stages a marker assignment for deletion (caller must call IUnitOfWork.SaveChangesAsync).</summary>
    void Remove(MarkerAssignment assignment);
}

public sealed record AliasRangeDto(int AliasStart, int AliasEnd);
