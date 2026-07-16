namespace ReportingService.Application.Interfaces;

public interface IReportFileService
{
    /// <summary>
    /// Expects a CSV/XLSX with header columns: AliasNumber, StudentCode, StudentName.
    /// Rows with a missing/unparseable AliasNumber are skipped.
    /// </summary>
    IReadOnlyDictionary<int, AliasMappingRow> ParseAliasMapping(Stream stream, string fileName);

    byte[] BuildReport(IReadOnlyList<Dictionary<string, object>> rows);
}

public sealed record AliasMappingRow(string StudentCode, string StudentName);
