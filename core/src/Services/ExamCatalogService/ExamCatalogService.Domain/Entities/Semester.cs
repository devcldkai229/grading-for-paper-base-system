using Contracts.Domain;

namespace ExamCatalogService.Domain.Entities;

public class Semester : Entity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsActive { get; set; }

    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
}
