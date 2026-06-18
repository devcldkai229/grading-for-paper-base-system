using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = Persistence.NpgsqlNotificationConfiguration.CreateDataSource(connectionString);
        services.AddSingleton(dataSource);
        services.AddDbContext<Persistence.NotificationDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MapEnum<NotificationChannel>("notification_channel");
                npgsql.MapEnum<NotificationStatus>("notification_status");
                npgsql.MapEnum<NotificationType>("notification_type");
            }));

        services.AddSingleton(new NotificationDatabaseSettings(connectionString));

        return services;
    }

    public static async Task MigrateNotificationDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.NotificationDbContext>();
        var databaseSettings = scope.ServiceProvider.GetRequiredService<NotificationDatabaseSettings>();

        await EnsureDatabaseExistsAsync(databaseSettings.ConnectionString);
        await context.Database.MigrateAsync();
    }

    private static async Task EnsureDatabaseExistsAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Notification database connection string is missing.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new InvalidOperationException("Notification database name is missing.");
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
