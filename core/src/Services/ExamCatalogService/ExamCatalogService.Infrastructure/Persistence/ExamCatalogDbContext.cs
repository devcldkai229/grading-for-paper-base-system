using ExamCatalogService.Domain.Entities;
using ExamCatalogService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ExamCatalogService.Infrastructure.Persistence;

public class ExamCatalogDbContext : DbContext
{
    public ExamCatalogDbContext(DbContextOptions<ExamCatalogDbContext> options)
        : base(options)
    {
    }

    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Question> Questions => Set<Question>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.HasPostgresEnum<ExamType>(name: "exam_type");
        modelBuilder.HasPostgresEnum<SubjectStatus>(name: "subject_status");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExamCatalogDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
