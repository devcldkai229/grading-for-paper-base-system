using BuildingBlocks.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        var logger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .CreateLogger("Notification.DatabaseMigration");

        await PostgresDatabaseMigrator.MigrateAsync(
            context, databaseSettings.ConnectionString, logger);
    }
}
