using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using ExamCatalogService.Domain.Entities;
using ExamCatalogService.Domain.Enums;
using ExamCatalogService.Infrastructure.Persistence;
using ExamCatalogService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ExamCatalogService.Infrastructure.Repositories;

public class ExamCatalogRepository : IExamCatalogRepository
{
    private readonly ExamCatalogDbContext _context;

    public ExamCatalogRepository(ExamCatalogDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<SemesterDto> Items, int TotalCount)> GetSemestersAsync(
        bool? active, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Semesters.AsNoTracking().AsQueryable();

        if (active.HasValue)
        {
            query = query.Where(s => s.IsActive == active.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SemesterDto(
                s.Id,
                s.Code,
                s.Name,
                s.Description,
                s.StartDate,
                s.EndDate,
                s.IsActive,
                s.CreatedAt
            ))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<SemesterDto?> GetSemesterByIdAsync(Guid semesterId, CancellationToken ct = default)
    {
        return await _context.Semesters
            .AsNoTracking()
            .Where(s => s.Id == semesterId)
            .Select(s => new SemesterDto(
                s.Id,
                s.Code,
                s.Name,
                s.Description,
                s.StartDate,
                s.EndDate,
                s.IsActive,
                s.CreatedAt
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(IReadOnlyList<ExamDto> Items, int TotalCount)> GetExamsBySemesterAsync(
        Guid semesterId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Exams
            .AsNoTracking()
            .Where(e => e.SemesterId == semesterId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ExamDto(
                e.Id,
                e.SemesterId,
                e.Name,
                e.ExamType.ToString(),
                e.StartDate,
                e.EndDate,
                e.Subjects.Count,
                e.CreatedAt
            ))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<ExamDto?> GetExamByIdAsync(Guid examId, CancellationToken ct = default)
    {
        return await _context.Exams
            .AsNoTracking()
            .Where(e => e.Id == examId)
            .Select(e => new ExamDto(
                e.Id,
                e.SemesterId,
                e.Name,
                e.ExamType.ToString(),
                e.StartDate,
                e.EndDate,
                e.Subjects.Count,
                e.CreatedAt
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(IReadOnlyList<SubjectSummaryDto> Items, int TotalCount)> GetSubjectsByExamAsync(
        Guid examId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Subjects
            .AsNoTracking()
            .Where(s => s.ExamId == examId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(s => s.SubjectCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SubjectSummaryDto(
                s.Id,
                s.ExamId,
                s.SubjectCode,
                s.Title,
                s.MaxScore,
                s.Status.ToString(),
                s.ExamPaperS3Key != null,
                s.RubricS3Key != null,
                s.Questions.Count,
                s.CreatedAt
            ))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<SubjectDetailDto?> GetSubjectDetailAsync(
        Guid subjectId, CancellationToken ct = default)
    {
        return await _context.Subjects
            .AsNoTracking()
            .Where(s => s.Id == subjectId)
            .Select(s => new SubjectDetailDto(
                s.Id,
                s.ExamId,
                s.SubjectCode,
                s.Title,
                s.MaxScore,
                s.Status.ToString(),
                s.ExamPaperS3Key != null,
                s.ExamPaperFileName,
                s.ExamPaperContentType,
                s.RubricS3Key != null,
                s.RubricFileName,
                s.RubricContentType,
                s.RubricVersion,
                s.Questions
                    .OrderBy(q => q.OrderIndex)
                    .Select(q => new QuestionDto(
                        q.Id,
                        q.QuestionNumber,
                        q.GroupLabel,
                        q.Label,
                        q.MaxScore,
                        q.OrderIndex
                    ))
                    .ToList(),
                s.CreatedAt
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(string S3Key, string FileName, string ContentType)?> GetExamPaperInfoAsync(
        Guid subjectId, CancellationToken ct = default)
    {
        var result = await _context.Subjects
            .AsNoTracking()
            .Where(s => s.Id == subjectId)
            .Select(s => new
            {
                s.ExamPaperS3Key,
                s.ExamPaperFileName,
                s.ExamPaperContentType
            })
            .FirstOrDefaultAsync(ct);

        if (result is null || result.ExamPaperS3Key is null ||
            result.ExamPaperFileName is null || result.ExamPaperContentType is null)
        {
            return null;
        }

        return (result.ExamPaperS3Key, result.ExamPaperFileName, result.ExamPaperContentType);
    }

    public async Task<(string S3Key, string FileName, string ContentType, string OriginalContentType)?> GetExamPaperViewInfoAsync(
        Guid subjectId, CancellationToken ct = default)
    {
        var result = await _context.Subjects
            .AsNoTracking()
            .Where(s => s.Id == subjectId)
            .Select(s => new
            {
                s.ExamPaperS3Key,
                s.ExamPaperFileName,
                s.ExamPaperContentType,
                s.ExamPaperPreviewS3Key,
                s.ExamPaperPreviewContentType
            })
            .FirstOrDefaultAsync(ct);

        if (result is null || result.ExamPaperS3Key is null ||
            result.ExamPaperFileName is null || result.ExamPaperContentType is null)
        {
            return null;
        }

        var previewKey = result.ExamPaperPreviewS3Key ?? result.ExamPaperS3Key;
        var previewContentType = result.ExamPaperPreviewContentType ?? result.ExamPaperContentType;
        var displayName = SubjectFilePreviewService.GetPreviewDisplayFileName(
            result.ExamPaperFileName, previewContentType);

        return (previewKey, displayName, previewContentType, result.ExamPaperContentType);
    }

    public async Task<(string S3Key, string FileName, string ContentType)?> GetExamPaperOriginalInfoAsync(
        Guid subjectId, CancellationToken ct = default)
    {
        var result = await _context.Subjects
            .AsNoTracking()
            .Where(s => s.Id == subjectId)
            .Select(s => new
            {
                s.ExamPaperS3Key,
                s.ExamPaperFileName,
                s.ExamPaperContentType
            })
            .FirstOrDefaultAsync(ct);

        if (result is null || result.ExamPaperS3Key is null ||
            result.ExamPaperFileName is null || result.ExamPaperContentType is null)
        {
            return null;
        }

        if (!SubjectFilePreviewService.IsDocx(result.ExamPaperContentType))
        {
            return null;
        }

        return (result.ExamPaperS3Key, result.ExamPaperFileName, result.ExamPaperContentType);
    }

    public async Task<(string S3Key, string FileName, string ContentType, int RubricVersion)?> GetRubricInfoAsync(
        Guid subjectId, CancellationToken ct = default)
    {
        var result = await _context.Subjects
            .AsNoTracking()
            .Where(s => s.Id == subjectId)
            .Select(s => new
            {
                s.RubricS3Key,
                s.RubricFileName,
                s.RubricContentType,
                s.RubricVersion
            })
            .FirstOrDefaultAsync(ct);

        if (result is null || result.RubricS3Key is null ||
            result.RubricFileName is null || result.RubricContentType is null)
        {
            return null;
        }

        return (result.RubricS3Key, result.RubricFileName, result.RubricContentType, result.RubricVersion);
    }

    public async Task<(string S3Key, string FileName, string ContentType, string OriginalContentType, int RubricVersion)?> GetRubricViewInfoAsync(
        Guid subjectId, CancellationToken ct = default)
    {
        var result = await _context.Subjects
            .AsNoTracking()
            .Where(s => s.Id == subjectId)
            .Select(s => new
            {
                s.RubricS3Key,
                s.RubricFileName,
                s.RubricContentType,
                s.RubricPreviewS3Key,
                s.RubricPreviewContentType,
                s.RubricVersion
            })
            .FirstOrDefaultAsync(ct);

        if (result is null || result.RubricS3Key is null ||
            result.RubricFileName is null || result.RubricContentType is null)
        {
            return null;
        }

        var previewKey = result.RubricPreviewS3Key ?? result.RubricS3Key;
        var previewContentType = result.RubricPreviewContentType ?? result.RubricContentType;
        var displayName = SubjectFilePreviewService.GetPreviewDisplayFileName(
            result.RubricFileName, previewContentType);

        return (previewKey, displayName, previewContentType, result.RubricContentType, result.RubricVersion);
    }

    public async Task<(string S3Key, string FileName, string ContentType, int RubricVersion)?> GetRubricOriginalInfoAsync(
        Guid subjectId, CancellationToken ct = default)
    {
        var result = await _context.Subjects
            .AsNoTracking()
            .Where(s => s.Id == subjectId)
            .Select(s => new
            {
                s.RubricS3Key,
                s.RubricFileName,
                s.RubricContentType,
                s.RubricVersion
            })
            .FirstOrDefaultAsync(ct);

        if (result is null || result.RubricS3Key is null ||
            result.RubricFileName is null || result.RubricContentType is null)
        {
            return null;
        }

        if (!SubjectFilePreviewService.IsDocx(result.RubricContentType))
        {
            return null;
        }

        return (result.RubricS3Key, result.RubricFileName, result.RubricContentType, result.RubricVersion);
    }

    public async Task<SemesterDto> CreateSemesterAsync(
        CreateSemesterRequest request, CancellationToken ct = default)
    {
        var semester = new Semester
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive
        };

        _context.Semesters.Add(semester);
        await _context.SaveChangesAsync(ct);

        return MapSemester(semester);
    }

    public async Task<SemesterDto?> UpdateSemesterAsync(
        Guid semesterId, UpdateSemesterRequest request, CancellationToken ct = default)
    {
        var semester = await _context.Semesters.FirstOrDefaultAsync(s => s.Id == semesterId, ct);
        if (semester is null) return null;

        semester.Code = request.Code.Trim();
        semester.Name = request.Name.Trim();
        semester.Description = request.Description;
        semester.StartDate = request.StartDate;
        semester.EndDate = request.EndDate;
        semester.IsActive = request.IsActive;
        await _context.SaveChangesAsync(ct);
        return MapSemester(semester);
    }

    public async Task<bool> DeleteSemesterAsync(Guid semesterId, CancellationToken ct = default)
    {
        var semester = await _context.Semesters.FirstOrDefaultAsync(s => s.Id == semesterId, ct);
        if (semester is null) return false;

        _context.Semesters.Remove(semester);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ExamDto> CreateExamAsync(CreateExamRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ExamType>(request.ExamType, ignoreCase: true, out var examType))
        {
            examType = ExamType.FE;
        }

        var exam = new Exam
        {
            SemesterId = request.SemesterId,
            Name = request.Name.Trim(),
            ExamType = examType,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        _context.Exams.Add(exam);
        await _context.SaveChangesAsync(ct);

        return await GetExamByIdAsync(exam.Id, ct)
            ?? throw new InvalidOperationException("Failed to load created exam.");
    }

    public async Task<ExamDto?> UpdateExamAsync(
        Guid examId, UpdateExamRequest request, CancellationToken ct = default)
    {
        var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId, ct);
        if (exam is null) return null;

        if (!Enum.TryParse<ExamType>(request.ExamType, ignoreCase: true, out var examType))
        {
            examType = exam.ExamType;
        }

        exam.Name = request.Name.Trim();
        exam.ExamType = examType;
        exam.StartDate = request.StartDate;
        exam.EndDate = request.EndDate;
        await _context.SaveChangesAsync(ct);
        return await GetExamByIdAsync(examId, ct);
    }

    public async Task<SubjectDetailDto> CreateSubjectAsync(
        CreateSubjectRequest request, CancellationToken ct = default)
    {
        var status = SubjectStatus.Draft;
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<SubjectStatus>(request.Status, ignoreCase: true, out var parsedStatus))
        {
            status = parsedStatus;
        }

        var subject = new Subject
        {
            ExamId = request.ExamId,
            SubjectCode = request.SubjectCode.Trim(),
            Title = request.Title,
            MaxScore = request.MaxScore,
            Status = status,
            RubricVersion = 1
        };

        _context.Subjects.Add(subject);
        await _context.SaveChangesAsync(ct);

        return await GetSubjectDetailAsync(subject.Id, ct)
            ?? throw new InvalidOperationException("Failed to load created subject.");
    }

    public async Task<SubjectDetailDto?> UpdateSubjectAsync(
        Guid subjectId, UpdateSubjectRequest request, CancellationToken ct = default)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId, ct);
        if (subject is null) return null;

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<SubjectStatus>(request.Status, ignoreCase: true, out var parsedStatus))
        {
            subject.Status = parsedStatus;
        }

        subject.SubjectCode = request.SubjectCode.Trim();
        subject.Title = request.Title;
        subject.MaxScore = request.MaxScore;
        await _context.SaveChangesAsync(ct);
        return await GetSubjectDetailAsync(subjectId, ct);
    }

    public async Task<bool> UpdateExamPaperAsync(
        Guid subjectId,
        string s3Key,
        string fileName,
        string contentType,
        string previewS3Key,
        string previewContentType,
        CancellationToken ct = default)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId, ct);
        if (subject is null) return false;

        subject.ExamPaperS3Key = s3Key;
        subject.ExamPaperFileName = fileName;
        subject.ExamPaperContentType = contentType;
        subject.ExamPaperPreviewS3Key = previewS3Key;
        subject.ExamPaperPreviewContentType = previewContentType;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int?> UpdateRubricAsync(
        Guid subjectId,
        string s3Key,
        string fileName,
        string contentType,
        string previewS3Key,
        string previewContentType,
        CancellationToken ct = default)
    {
        var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId, ct);
        if (subject is null) return null;

        subject.RubricVersion = subject.RubricS3Key is null ? 1 : subject.RubricVersion + 1;
        subject.RubricS3Key = s3Key;
        subject.RubricFileName = fileName;
        subject.RubricContentType = contentType;
        subject.RubricPreviewS3Key = previewS3Key;
        subject.RubricPreviewContentType = previewContentType;
        await _context.SaveChangesAsync(ct);
        return subject.RubricVersion;
    }

    public async Task<IReadOnlyList<QuestionDto>?> ReplaceQuestionsAsync(
        Guid subjectId, IReadOnlyList<QuestionInputDto> questions, CancellationToken ct = default)
    {
        var subject = await _context.Subjects
            .Include(s => s.Questions)
            .FirstOrDefaultAsync(s => s.Id == subjectId, ct);

        if (subject is null) return null;

        _context.Questions.RemoveRange(subject.Questions);

        var newQuestions = questions
            .OrderBy(q => q.OrderIndex)
            .Select(q => new Question
            {
                SubjectId = subjectId,
                GroupLabel = TruncateOptional(q.GroupLabel, 100),
                QuestionNumber = TruncateRequired(q.QuestionNumber, 20),
                Label = TruncateOptional(q.Label, 255),
                MaxScore = q.MaxScore,
                OrderIndex = q.OrderIndex
            })
            .ToList();

        _context.Questions.AddRange(newQuestions);
        await _context.SaveChangesAsync(ct);

        return newQuestions
            .OrderBy(q => q.OrderIndex)
            .Select(q => new QuestionDto(
                q.Id,
                q.QuestionNumber,
                q.GroupLabel,
                q.Label,
                q.MaxScore,
                q.OrderIndex))
            .ToList();
    }

    private static string? TruncateOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string TruncateRequired(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static SemesterDto MapSemester(Semester semester) =>
        new(
            semester.Id,
            semester.Code,
            semester.Name,
            semester.Description,
            semester.StartDate,
            semester.EndDate,
            semester.IsActive,
            semester.CreatedAt
        );
}
