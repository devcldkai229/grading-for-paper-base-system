namespace GradingService.Application.Interfaces;

/// <summary>Builds a downloadable spreadsheet from tabular rows (column name -> value).</summary>
public interface IGradeExportFileBuilder
{
    byte[] Build(IReadOnlyList<Dictionary<string, object>> rows);
}
