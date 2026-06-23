using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BuildingBlocks.EfCore;

/// <summary>
/// Bootstraps PostgreSQL databases for EF Core migrations on first run.
/// Creates the database and migrations history table before <see cref="DbContext.Database.MigrateAsync"/>,
/// avoiding failed queries against a missing <c>__EFMigrationsHistory</c> table.
/// </summary>
public static class PostgresDatabaseMigrator
{
    public static async Task MigrateAsync<TContext>(
        TContext context,
        string connectionString,
        ILogger logger,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        await EnsureDatabaseExistsAsync(connectionString, cancellationToken);

        var historyRepository = context.GetService<IHistoryRepository>();
        if (!historyRepository.Exists())
        {
            logger.LogInformation(
                "First run for {Context}: creating EF migrations history table...",
                typeof(TContext).Name);
            historyRepository.Create();
        }

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation(
                "Database for {Context} is up to date (no pending migrations).",
                typeof(TContext).Name);
            return;
        }

        logger.LogInformation(
            "Applying {Count} migration(s) for {Context}: {Names}",
            pending.Count,
            typeof(TContext).Name,
            string.Join(", ", pending));

        await context.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Migrations for {Context} applied successfully.", typeof(TContext).Name);
    }

    public static async Task EnsureDatabaseExistsAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection string is missing.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new InvalidOperationException("Database name is missing in the connection string.");
        }

        var targetDatabase = builder.Database;
        var maintenanceBuilder = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
        {
            Database = "postgres"
        };

        await using var connection = new NpgsqlConnection(maintenanceBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT 1 FROM pg_database WHERE datname = @databaseName";
            checkCommand.Parameters.AddWithValue("databaseName", targetDatabase);

            var existingDatabase = await checkCommand.ExecuteScalarAsync(cancellationToken);
            if (existingDatabase is not null)
            {
                return;
            }
        }

        var quotedDatabaseName = QuoteIdentifier(targetDatabase);
        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"CREATE DATABASE {quotedDatabaseName}";
        await createCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"")}\"";
}
