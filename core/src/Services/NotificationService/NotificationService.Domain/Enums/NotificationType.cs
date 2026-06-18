using NpgsqlTypes;

namespace NotificationService.Domain.Enums;

public enum NotificationType
{
    [PgName("assignment")]
    Assignment,

    [PgName("deadline_reminder")]
    DeadlineReminder,

    [PgName("regrade_request")]
    RegradeRequest,

    [PgName("export_ready")]
    ExportReady,

    [PgName("system")]
    System
}
