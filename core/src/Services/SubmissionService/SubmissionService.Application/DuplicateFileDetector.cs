using SubmissionService.Application.DTOs;

namespace SubmissionService.Application;

/// <summary>File to be checked for content-hash duplicates within a batch.</summary>
public record HashedFileEntry(
    string ContentHash,
    Guid PaperId,
    string? StudentAlias,
    string? FileName
);

/// <summary>
/// Pure content-hash duplicate detection. Never blocks ingestion — callers only use the
/// result to surface a warning.
/// </summary>
public static class DuplicateFileDetector
{
    public static IReadOnlyList<DuplicateFileWarningDto> Detect(IEnumerable<HashedFileEntry> files)
    {
        return files
            .GroupBy(f => f.ContentHash, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateFileWarningDto(
                g.Key,
                g.Select(f => new DuplicateFileEntryDto(f.PaperId, f.StudentAlias, f.FileName)).ToList()))
            .ToList();
    }
}
