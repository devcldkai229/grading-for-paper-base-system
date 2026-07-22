namespace GradingService.Application.Interfaces;

/// <summary>
/// Reads the local lecturer marker-code projection (replicated from IamService via
/// LecturerProfileChanged). Replaces the old synchronous bulk HTTP pull from IamService.
/// </summary>
public interface ILecturerMarkerCodeRepository
{
    /// <summary>Map of teacher id -> marker code, only for lecturers that have a marker code set.</summary>
    Task<Dictionary<Guid, string>> GetAllMarkerCodesAsync(CancellationToken ct = default);

    /// <summary>Idempotent upsert of a single lecturer's replicated profile.</summary>
    Task UpsertAsync(Guid userId, string? markerCode, string? fullName, DateTime updatedAt, CancellationToken ct = default);
}
