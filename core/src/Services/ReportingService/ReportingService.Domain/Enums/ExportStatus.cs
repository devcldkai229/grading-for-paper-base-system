using NpgsqlTypes;

namespace ReportingService.Domain.Enums;

public enum ExportStatus
{
    [PgName("queued")]
    Queued,

    [PgName("processing")]
    Processing,

    [PgName("completed")]
    Completed,

    [PgName("failed")]
    Failed
}
