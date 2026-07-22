using ExamCatalogService.Domain.Entities;
using ExamCatalogService.Domain.Enums;
using MassTransit;
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
    public DbSet<ScoreGridTemplate> ScoreGridTemplates => Set<ScoreGridTemplate>();

    /// <summary>Compiled grading contracts (check-items + partial-credit) per subject/rubric version.</summary>
    public DbSet<GradingContract> GradingContracts => Set<GradingContract>();

    /// <summary>Illustration/table crops referenced by compiled contracts.</summary>
    public DbSet<RubricAsset> RubricAssets => Set<RubricAsset>();

    /// <summary>Local projection of GradingService marker assignments (via MarkerAssignmentChanged).</summary>
    public DbSet<MarkerAssignmentView> MarkerAssignmentViews => Set<MarkerAssignmentView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.HasPostgresEnum<ExamType>(name: "exam_type");
        modelBuilder.HasPostgresEnum<SubjectStatus>(name: "subject_status");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExamCatalogDbContext).Assembly);

        // MassTransit transactional outbox/inbox tables (N7 outbox, N8 idempotency).
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        base.OnModelCreating(modelBuilder);
    }
}
