using BuildingBlocks.EfCore;
using BuildingBlocks.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReportingService.Domain.Enums;

namespace ReportingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReportingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = Persistence.NpgsqlReportingConfiguration.CreateDataSource(connectionString);
        services.AddSingleton(dataSource);
        services.AddDbContext<Persistence.ReportingDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MapEnum<ExportStatus>("export_status");
            }));

        services.AddSingleton(new ReportingDatabaseSettings(connectionString));

        return services;
    }

    public static async Task MigrateReportingDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.ReportingDbContext>();
        var databaseSettings = scope.ServiceProvider.GetRequiredService<ReportingDatabaseSettings>();
        var logger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .CreateLogger("Reporting.DatabaseMigration");

        await PostgresDatabaseMigrator.MigrateAsync(
            context, databaseSettings.ConnectionString, logger);
    }
}
