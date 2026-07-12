using System.Globalization;
using MiniExcelLibs;
using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;
using ReportingService.Domain.Entities;
using ReportingService.Domain.Enums;
using ReportingService.Infrastructure.Persistence;

namespace ReportingService.Infrastructure.Services;

public class FeedbackReportService : IFeedbackReportService
{
    private readonly IGradingServiceClient _gradingClient;
    private readonly ReportingDbContext _db;

    public FeedbackReportService(IGradingServiceClient gradingClient, ReportingDbContext db)
    {
        _gradingClient = gradingClient;
        _db = db;
    }

    private sealed record MappingRow(string StudentCode, string StudentName);

    public async Task<(byte[]? FileBytes, string? FileName, string? Error)> GenerateFeedbackReportAsync(
        Guid subjectId,
        Guid requestedBy,
        Stream aliasMappingFile,
        string aliasMappingFileName,
        CancellationToken ct = default)
    {
        Dictionary<int, MappingRow> mapping;
        try
        {
            mapping = ParseAliasMapping(aliasMappingFile, aliasMappingFileName);
        }
        catch (Exception ex)
        {
            return (null, null, $"Không đọc được file mapping: {ex.Message}");
        }

        if (mapping.Count == 0)
        {
            return (null, null,
                "File mapping rỗng hoặc thiếu cột bắt buộc (AliasNumber, StudentCode, StudentName).");
        }

        var feedback = await _gradingClient.GetReleasableFeedbackAsync(subjectId, ct);
        if (feedback is null)
        {
            return (null, null, "Không thể lấy dữ liệu chấm bài từ GradingService. Vui lòng thử lại.");
        }

        if (feedback.Students.Count == 0)
        {
            return (null, null, "Chưa có bài nào đã nộp (Submitted) cho môn này để xuất báo cáo.");
        }

        var questionNumbers = feedback.Students
            .SelectMany(s => s.Questions)
            .GroupBy(q => q.QuestionNumber)
            .Select(g => g.Key)
            .ToList();

        var rows = new List<Dictionary<string, object>>();
        foreach (var student in feedback.Students)
        {
            var matched = student.AliasNumber.HasValue && mapping.TryGetValue(student.AliasNumber.Value, out var m)
                ? m
                : null;

            var row = new Dictionary<string, object>
            {
                ["MSSV"] = matched?.StudentCode ?? "",
                ["Họ tên"] = matched?.StudentName
                    ?? $"Chưa xác định (Alias {student.AliasNumber?.ToString(CultureInfo.InvariantCulture) ?? student.StudentAlias})",
                ["Alias"] = student.StudentAlias ?? "",
                ["Tổng điểm"] = student.TotalScore,
                ["Nhận xét chung"] = student.PaperComment ?? ""
            };

            foreach (var qNum in questionNumbers)
            {
                var q = student.Questions.FirstOrDefault(x => x.QuestionNumber == qNum);
                row[$"Điểm - Câu {qNum}"] = q?.Score ?? 0;
                row[$"Nhận xét - Câu {qNum}"] = q?.QuestionComment ?? "";
            }

            rows.Add(row);
        }

        using var memoryStream = new MemoryStream();
        MiniExcel.SaveAs(memoryStream, rows);
        var fileBytes = memoryStream.ToArray();
        var fileName = $"Feedback_{subjectId:N}.xlsx";

        _db.ExportJobs.Add(new ExportJob
        {
            SubjectId = subjectId,
            RequestedBy = requestedBy,
            Status = ExportStatus.Completed,
            ResultFileName = fileName,
            CompletedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return (fileBytes, fileName, null);
    }

    /// <summary>
    /// Expects a CSV/XLSX with header columns: AliasNumber, StudentCode, StudentName.
    /// Rows with a missing/unparseable AliasNumber are skipped.
    /// </summary>
    private static Dictionary<int, MappingRow> ParseAliasMapping(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var excelType = ext == ".csv" ? ExcelType.CSV : ExcelType.XLSX;

        var result = new Dictionary<int, MappingRow>();
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

            result[aliasNumber] = new MappingRow(studentCode, studentName);
        }

        return result;
    }
}
