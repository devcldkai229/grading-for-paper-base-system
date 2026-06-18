using NpgsqlTypes;

namespace GradingService.Domain.Enums;

public enum GradingProgressStatus
{
    [PgName("not_started")]
    NotStarted,

    [PgName("drafting")]
    Drafting,

    [PgName("submitted")]
    Submitted
}
