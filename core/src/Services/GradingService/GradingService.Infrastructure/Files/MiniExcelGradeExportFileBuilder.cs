using GradingService.Application.Interfaces;

namespace GradingService.Infrastructure.Files;

public class MiniExcelGradeExportFileBuilder : IGradeExportFileBuilder
{
    public byte[] Build(IReadOnlyList<Dictionary<string, object>> rows)
    {
        using var memoryStream = new MemoryStream();
        MiniExcelLibs.MiniExcel.SaveAs(memoryStream, rows);
        return memoryStream.ToArray();
    }
}
