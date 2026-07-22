using System.Globalization;
using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;
using ReportingService.Domain.Entities;
using ReportingService.Domain.Enums;

namespace ReportingService.Application.Services;

public class FeedbackReportService : IFeedbackReportService
{
    private readonly IFeedbackRecordReadRepository _feedbackRepository;
    private readonly IExportJobRepository _exportJobRepository;
    private readonly IReportFileService _reportFileService;

    public FeedbackReportService(
        IFeedbackRecordReadRepository feedbackRepository,
        IExportJobRepository exportJobRepository,
        IReportFileService reportFileService)
    {
        _feedbackRepository = feedbackRepository;
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

        // Releasable feedback == submitted feedback, now served from the local feedback_record projection
        // (kept in sync by Grading's ScoreSubmitted/Overridden events) instead of a REST call to Grading.
        var records = await _feedbackRepository.GetBySubjectAsync(subjectId, ct);
        if (records.Count == 0)
        {
            return (null, null, "Chưa có bài nào đã nộp (Submitted) cho môn này để xuất báo cáo.");
        }

        var students = records
            .Select(r => new
            {
                r.AliasNumber,
                r.StudentAlias,
                r.TotalScore,
                r.PaperComment,
                Questions = FeedbackQuestionsJson.Deserialize(r.QuestionsJson)
            })
            .ToList();

        var questionNumbers = students
            .SelectMany(s => s.Questions)
            .GroupBy(q => q.QuestionNumber)
            .Select(g => g.Key)
            .ToList();

        var rows = new List<Dictionary<string, object>>();
        foreach (var student in students)
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
