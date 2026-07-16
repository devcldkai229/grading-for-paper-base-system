namespace ReportingService.Application.Interfaces;

public interface IFeedbackReportService
{
    /// <summary>
    /// Builds a per-student, releasable-feedback-only export (one row per student), de-anonymised
    /// via an admin-supplied alias↔student mapping file (columns: AliasNumber, StudentCode, StudentName).
    /// Returns an error message instead of throwing for expected failure cases (no submitted papers,
    /// GradingService unreachable, unparseable mapping file).
    /// </summary>
    Task<(byte[]? FileBytes, string? FileName, string? Error)> GenerateFeedbackReportAsync(
        Guid subjectId,
        Guid requestedBy,
        Stream aliasMappingFile,
        string aliasMappingFileName,
        CancellationToken ct = default);
}
