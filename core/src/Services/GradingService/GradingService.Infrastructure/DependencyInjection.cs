using GradingService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GradingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGradingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = Persistence.NpgsqlGradingConfiguration.CreateDataSource(connectionString);
        services.AddSingleton(dataSource);
        services.AddDbContext<Persistence.GradingDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MapEnum<AssignmentType>("assignment_type");
                npgsql.MapEnum<GradingProgressStatus>("grading_progress_status");
                npgsql.MapEnum<AiSyncStatus>("ai_sync_status");
                npgsql.MapEnum<AiReviewStatus>("ai_review_status");
            }));

        services.AddSingleton(new GradingDatabaseSettings(connectionString));

        return services;
    }

    public static async Task MigrateGradingDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.GradingDbContext>();
        var databaseSettings = scope.ServiceProvider.GetRequiredService<GradingDatabaseSettings>();

        await EnsureDatabaseExistsAsync(databaseSettings.ConnectionString);
        await context.Database.MigrateAsync();
    }

    private static async Task EnsureDatabaseExistsAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Grading database connection string is missing.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new InvalidOperationException("Grading database name is missing.");
        }

        var targetDatabase = builder.Database;
        var maintenanceBuilder = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
        {
            Database = "postgres"
        };

        await using var connection = new NpgsqlConnection(maintenanceBuilder.ConnectionString);
        await connection.OpenAsync();

        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT 1 FROM pg_database WHERE datname = @databaseName";
            checkCommand.Parameters.AddWithValue("databaseName", targetDatabase);

            var existingDatabase = await checkCommand.ExecuteScalarAsync();
            if (existingDatabase is not null)
            {
                return;
            }
        }

        var quotedDatabaseName = QuoteIdentifier(targetDatabase);
        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"CREATE DATABASE {quotedDatabaseName}";
        await createCommand.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }
}
