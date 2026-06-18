using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ExamCatalogService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddExamCatalogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = Persistence.NpgsqlExamCatalogConfiguration.CreateDataSource(connectionString);
        services.AddSingleton(dataSource);
        services.AddDbContext<Persistence.ExamCatalogDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MapEnum<Domain.Enums.ExamType>("exam_type");
                npgsql.MapEnum<Domain.Enums.SubjectStatus>("subject_status");
            }));

        services.AddSingleton(new ExamCatalogDatabaseSettings(connectionString));

        return services;
    }

    public static async Task MigrateExamCatalogDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.ExamCatalogDbContext>();
        var databaseSettings = scope.ServiceProvider.GetRequiredService<ExamCatalogDatabaseSettings>();

        await EnsureDatabaseExistsAsync(databaseSettings.ConnectionString);
        await context.Database.MigrateAsync();
    }

    private static async Task EnsureDatabaseExistsAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Exam catalog database connection string is missing.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new InvalidOperationException("Exam catalog database name is missing.");
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
