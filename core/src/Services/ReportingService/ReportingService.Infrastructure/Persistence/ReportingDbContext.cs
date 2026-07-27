using ReportingService.Domain.Entities;
using ReportingService.Domain.Enums;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace ReportingService.Infrastructure.Persistence;

public class ReportingDbContext : DbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options)
        : base(options)
    {
    }

    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();
    public DbSet<MarkerProgress> MarkerProgress => Set<MarkerProgress>();
    public DbSet<SubjectProgress> SubjectProgress => Set<SubjectProgress>();
    public DbSet<AssignmentProgress> AssignmentProgress => Set<AssignmentProgress>();
    public DbSet<ScoreRecord> ScoreRecords => Set<ScoreRecord>();
    public DbSet<FeedbackRecord> FeedbackRecords => Set<FeedbackRecord>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<ExportStatus>(name: "export_status");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingDbContext).Assembly);

        // MassTransit inbox/outbox tables so event consumers dedupe redeliveries (N8). Reporting only
        // consumes, but the EF outbox integration requires all three entities to be mapped.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        base.OnModelCreating(modelBuilder);
    }
}
