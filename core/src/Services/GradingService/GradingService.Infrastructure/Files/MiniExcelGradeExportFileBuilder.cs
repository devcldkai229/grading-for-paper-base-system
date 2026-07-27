using GradingService.Application.Interfaces;
using MiniExcelLibs;

namespace GradingService.Infrastructure.Files;

public class MiniExcelGradeExportFileBuilder : IGradeExportFileBuilder
{
    public byte[] Build(IReadOnlyList<Dictionary<string, object>> rows)
    {
        using var memoryStream = new MemoryStream();
        MiniExcel.SaveAs(memoryStream, rows);
        return memoryStream.ToArray();
    }

    public byte[] Build(IEnumerable<Dictionary<string, object>> rows, bool printHeader)
    {
        using var memoryStream = new MemoryStream();
        MiniExcel.SaveAs(memoryStream, rows, printHeader: printHeader);
        return memoryStream.ToArray();
    }

    public byte[] BuildCsv(IEnumerable<Dictionary<string, object>> rows, bool printHeader = false)
    {
        using var memoryStream = new MemoryStream();
        MiniExcel.SaveAs(memoryStream, rows, printHeader: printHeader, excelType: ExcelType.CSV);
        return memoryStream.ToArray();
    }
}
