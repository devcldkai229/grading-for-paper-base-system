using NpgsqlTypes;

namespace NotificationService.Domain.Enums;

public enum NotificationStatus
{
    [PgName("pending")]
    Pending,

    [PgName("sent")]
    Sent,

    [PgName("failed")]
    Failed,

    [PgName("read")]
    Read
}
