using GradingService.Domain.Entities;
using GradingService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GradingService.Infrastructure.Persistence;

public class GradingDbContext : DbContext
{
    public GradingDbContext(DbContextOptions<GradingDbContext> options)
        : base(options)
    {
    }

    public DbSet<MarkerAssignment> MarkerAssignments => Set<MarkerAssignment>();
    public DbSet<GradingAssignment> GradingAssignments => Set<GradingAssignment>();
    public DbSet<GradingForm> GradingForms => Set<GradingForm>();
    public DbSet<QuestionGradeDetail> QuestionGradeDetails => Set<QuestionGradeDetail>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<AssignmentType>(name: "assignment_type");
        modelBuilder.HasPostgresEnum<GradingProgressStatus>(name: "grading_progress_status");
        modelBuilder.HasPostgresEnum<AiSyncStatus>(name: "ai_sync_status");
        modelBuilder.HasPostgresEnum<AiReviewStatus>(name: "ai_review_status");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GradingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
