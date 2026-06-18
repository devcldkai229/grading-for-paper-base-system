using ReportingService.Domain.Entities;
using ReportingService.Domain.Enums;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<ExportStatus>(name: "export_status");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
