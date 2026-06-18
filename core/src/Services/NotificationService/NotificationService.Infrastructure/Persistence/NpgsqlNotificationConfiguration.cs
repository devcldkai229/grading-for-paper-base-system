using NotificationService.Domain.Enums;
using Npgsql;

namespace NotificationService.Infrastructure.Persistence;

internal static class NpgsqlNotificationConfiguration
{
    public static void MapEnums(NpgsqlDataSourceBuilder builder)
    {
        builder.MapEnum<NotificationChannel>("notification_channel");
        builder.MapEnum<NotificationStatus>("notification_status");
        builder.MapEnum<NotificationType>("notification_type");
    }

    public static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        MapEnums(builder);
        return builder.Build();
    }
}
