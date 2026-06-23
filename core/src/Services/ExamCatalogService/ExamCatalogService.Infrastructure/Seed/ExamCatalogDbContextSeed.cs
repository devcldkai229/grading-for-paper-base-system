using ExamCatalogService.Domain.Entities;
using ExamCatalogService.Domain.Enums;
using ExamCatalogService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExamCatalogService.Infrastructure.Seed;

public class ExamCatalogDbContextSeed
{
    private static readonly IReadOnlyList<SemesterSeed> Semesters =
    [
        new(
            Code: "Spring2026",
            Name: "Spring 2026",
            StartDate: new DateOnly(2026, 1, 5),
            EndDate: new DateOnly(2026, 5, 31),
            IsActive: true,
            Subjects:
            [
                new("SWD392", "Software Architecture"),
                new("PMG201c", "Project Management")
            ]),
        new(
            Code: "Spring2025",
            Name: "Spring 2025",
            StartDate: new DateOnly(2025, 1, 6),
            EndDate: new DateOnly(2025, 5, 30),
            IsActive: false,
            Subjects:
            [
                new("SWE201c", "Software Engineering")
            ]),
        new(
            Code: "Summer2025",
            Name: "Summer 2025",
            StartDate: new DateOnly(2025, 5, 26),
            EndDate: new DateOnly(2025, 8, 22),
            IsActive: false,
            Subjects:
            [
                new("SWR302", "Software Requirement"),
                new("SWT301", "Software Testing")
            ]),
        new(
            Code: "Fall2025",
            Name: "Fall 2025",
            StartDate: new DateOnly(2025, 8, 25),
            EndDate: new DateOnly(2025, 12, 19),
            IsActive: false,
            Subjects:
            [
                new("ENW493c", "English Writing")
            ])
    ];

    public static async Task SeedAsync(ExamCatalogDbContext context, ILogger<ExamCatalogDbContextSeed> logger)
    {
        try
        {
            if (await context.Semesters.AnyAsync())
            {
                return;
            }

            logger.LogInformation("Seeding exam catalog (semesters, PE exams, subjects)...");

            foreach (var semesterSeed in Semesters)
            {
                var semester = new Semester
                {
                    Code = semesterSeed.Code,
                    Name = semesterSeed.Name,
                    Description = $"Seed semester {semesterSeed.Name}",
                    StartDate = semesterSeed.StartDate,
                    EndDate = semesterSeed.EndDate,
                    IsActive = semesterSeed.IsActive
                };

                var exam = new Exam
                {
                    Name = "Practical Examination",
                    ExamType = ExamType.PE,
                    StartDate = semesterSeed.StartDate,
                    EndDate = semesterSeed.EndDate
                };

                foreach (var subjectSeed in semesterSeed.Subjects)
                {
                    exam.Subjects.Add(new Subject
                    {
                        ExamId = exam.Id,
                        SubjectCode = subjectSeed.SubjectCode,
                        Title = subjectSeed.Title,
                        MaxScore = subjectSeed.MaxScore,
                        Status = SubjectStatus.Open,
                        RubricVersion = 1
                    });
                }

                semester.Exams.Add(exam);
                context.Semesters.Add(semester);
            }

            await context.SaveChangesAsync();
            logger.LogInformation(
                "Exam catalog seeded: {SemesterCount} semesters, {ExamCount} PE exams, {SubjectCount} subjects.",
                Semesters.Count,
                Semesters.Count,
                Semesters.Sum(s => s.Subjects.Count));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the exam catalog database.");
        }
    }

    private sealed record SemesterSeed(
        string Code,
        string Name,
        DateOnly StartDate,
        DateOnly EndDate,
        bool IsActive,
        IReadOnlyList<SubjectSeed> Subjects);

    private sealed record SubjectSeed(
        string SubjectCode,
        string? Title,
        decimal MaxScore = 10m);
}
