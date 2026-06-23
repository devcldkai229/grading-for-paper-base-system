using Contracts.Domain;
using ExamCatalogService.Domain.Enums;

namespace ExamCatalogService.Domain.Entities;

public class Subject : Entity
{
    public Guid ExamId { get; set; }

    public string SubjectCode { get; set; } = string.Empty;

    public string? Title { get; set; }

    public decimal MaxScore { get; set; } = 10;

    public string? ExamPaperS3Key { get; set; }

    public string? ExamPaperFileName { get; set; }

    public string? ExamPaperContentType { get; set; }

    public string? ExamPaperPreviewS3Key { get; set; }

    public string? ExamPaperPreviewContentType { get; set; }

    public string? RubricS3Key { get; set; }

    public string? RubricFileName { get; set; }

    public string? RubricContentType { get; set; }

    public string? RubricPreviewS3Key { get; set; }

    public string? RubricPreviewContentType { get; set; }

    public int RubricVersion { get; set; } = 1;

    public SubjectStatus Status { get; set; } = SubjectStatus.Draft;

    public Exam Exam { get; set; } = null!;

    public ICollection<Question> Questions { get; set; } = new List<Question>();
}
