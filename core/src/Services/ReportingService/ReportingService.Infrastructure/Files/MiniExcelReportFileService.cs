using System.Globalization;
using MiniExcelLibs;
using ReportingService.Application.Interfaces;

namespace ReportingService.Infrastructure.Files;

public class MiniExcelReportFileService : IReportFileService
{
    public IReadOnlyDictionary<int, AliasMappingRow> ParseAliasMapping(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var excelType = ext == ".csv" ? ExcelType.CSV : ExcelType.XLSX;

        var result = new Dictionary<int, AliasMappingRow>();
        foreach (var row in MiniExcel.Query(stream, useHeaderRow: true, excelType: excelType))
        {
            var dict = (IDictionary<string, object>)row;

            if (!dict.TryGetValue("AliasNumber", out var aliasRaw)
                || !int.TryParse(Convert.ToString(aliasRaw, CultureInfo.InvariantCulture), out var aliasNumber))
            {
                continue;
            }

            var studentCode = dict.TryGetValue("StudentCode", out var code)
                ? Convert.ToString(code, CultureInfo.InvariantCulture) ?? ""
                : "";
            var studentName = dict.TryGetValue("StudentName", out var name)
                ? Convert.ToString(name, CultureInfo.InvariantCulture) ?? ""
                : "";

            result[aliasNumber] = new AliasMappingRow(studentCode, studentName);
        }

        return result;
    }

    public byte[] BuildReport(IReadOnlyList<Dictionary<string, object>> rows)
    {
        using var memoryStream = new MemoryStream();
        MiniExcel.SaveAs(memoryStream, rows);
        return memoryStream.ToArray();
    }
}
