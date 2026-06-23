namespace SubmissionService.Application.Interfaces;

public record AliasRangeDto(int Start, int End);

public interface IGradingServiceClient
{
    /// <summary>
    /// Gets ALL assigned alias ranges for a lecturer on a subject (a lecturer may have several).
    /// Returns:
    ///   - non-null list (possibly empty) when GradingService answered. Empty => no assignment => no access.
    ///   - null when GradingService is unreachable/errored => caller MUST fail closed (deny access).
    /// </summary>
    Task<IReadOnlyList<AliasRangeDto>?> GetAliasRangesAsync(
        Guid subjectId, Guid lecturerId, CancellationToken ct = default);
}
