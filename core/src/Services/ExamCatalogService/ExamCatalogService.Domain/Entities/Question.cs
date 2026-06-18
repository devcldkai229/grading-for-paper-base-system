using Contracts.Domain;

namespace ExamCatalogService.Domain.Entities;

public class Question : Entity
{
    public Guid SubjectId { get; set; }

    public string QuestionNumber { get; set; } = string.Empty;

    public string? Label { get; set; }

    public decimal MaxScore { get; set; }

    public int OrderIndex { get; set; }

    public Subject Subject { get; set; } = null!;
}
