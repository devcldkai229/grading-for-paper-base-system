using NpgsqlTypes;

namespace GradingService.Domain.Enums;

public enum AiSyncStatus
{
    [PgName("not_requested")]
    NotRequested,

    [PgName("queued")]
    Queued,

    [PgName("processing")]
    Processing,

    [PgName("completed")]
    Completed,

    [PgName("failed")]
    Failed
}
