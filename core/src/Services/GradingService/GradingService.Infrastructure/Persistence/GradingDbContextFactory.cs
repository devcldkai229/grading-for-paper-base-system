using GradingService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GradingService.Infrastructure.Persistence;

public class GradingDbContextFactory : IDesignTimeDbContextFactory<GradingDbContext>
{
    public GradingDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../GradingService.API"))
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = NpgsqlGradingConfiguration.CreateDataSource(connectionString);
        var optionsBuilder = new DbContextOptionsBuilder<GradingDbContext>();
        optionsBuilder.UseNpgsql(dataSource, npgsql =>
        {
            npgsql.MapEnum<AssignmentType>("assignment_type");
            npgsql.MapEnum<GradingProgressStatus>("grading_progress_status");
            npgsql.MapEnum<AiSyncStatus>("ai_sync_status");
            npgsql.MapEnum<AiReviewStatus>("ai_review_status");
        });

        return new GradingDbContext(optionsBuilder.Options);
    }
}
