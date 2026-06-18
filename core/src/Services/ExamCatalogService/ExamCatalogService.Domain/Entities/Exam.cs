using Contracts.Domain;
using ExamCatalogService.Domain.Enums;

namespace ExamCatalogService.Domain.Entities;

public class Exam : Entity
{
    public Guid SemesterId { get; set; }

    public string Name { get; set; } = string.Empty;

    public ExamType ExamType { get; set; } = ExamType.FE;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public Semester Semester { get; set; } = null!;

    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
}
