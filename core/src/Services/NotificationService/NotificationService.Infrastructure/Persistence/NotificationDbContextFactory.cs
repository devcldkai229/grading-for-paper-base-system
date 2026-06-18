using NotificationService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace NotificationService.Infrastructure.Persistence;

public class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../NotificationService.API"))
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = NpgsqlNotificationConfiguration.CreateDataSource(connectionString);
        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
        optionsBuilder.UseNpgsql(dataSource, npgsql =>
        {
            npgsql.MapEnum<NotificationChannel>("notification_channel");
            npgsql.MapEnum<NotificationStatus>("notification_status");
            npgsql.MapEnum<NotificationType>("notification_type");
        });

        return new NotificationDbContext(optionsBuilder.Options);
    }
}
