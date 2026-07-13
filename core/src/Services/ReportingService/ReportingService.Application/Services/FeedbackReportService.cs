using System.Globalization;
using ReportingService.Application.Interfaces;
using ReportingService.Domain.Entities;
using ReportingService.Domain.Enums;

namespace ReportingService.Application.Services;

public class FeedbackReportService : IFeedbackReportService
{
    private readonly IGradingServiceClient _gradingClient;
    private readonly IExportJobRepository _exportJobRepository;
    private readonly IReportFileService _reportFileService;

    public FeedbackReportService(
        IGradingServiceClient gradingClient,
        IExportJobRepository exportJobRepository,
        IReportFileService reportFileService)
    {
        _gradingClient = gradingClient;
        _exportJobRepository = exportJobRepository;
        _reportFileService = reportFileService;
    }

    public async Task<(byte[]? FileBytes, string? FileName, string? Error)> GenerateFeedbackReportAsync(
        Guid subjectId,
        Guid requestedBy,
        Stream aliasMappingFile,
        string aliasMappingFileName,
        CancellationToken ct = default)
    {
        IReadOnlyDictionary<int, AliasMappingRow> mapping;
        try
        {
            mapping = _reportFileService.ParseAliasMapping(aliasMappingFile, aliasMappingFileName);
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

        var fileBytes = _reportFileService.BuildReport(rows);
        var fileName = $"Feedback_{subjectId:N}.xlsx";

        await _exportJobRepository.AddAsync(new ExportJob
        {
            SubjectId = subjectId,
            RequestedBy = requestedBy,
            Status = ExportStatus.Completed,
            ResultFileName = fileName,
            CompletedAt = DateTime.UtcNow
        }, ct);

        return (fileBytes, fileName, null);
    }
}
