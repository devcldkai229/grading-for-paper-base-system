using NpgsqlTypes;

namespace NotificationService.Domain.Enums;

public enum NotificationChannel
{
    [PgName("in_app")]
    InApp,

    [PgName("email")]
    Email
}
