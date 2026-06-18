using NpgsqlTypes;

namespace GradingService.Domain.Enums;

public enum AiReviewStatus
{
    [PgName("pending")]
    Pending,

    [PgName("accepted")]
    Accepted,

    [PgName("rejected")]
    Rejected,

    [PgName("modified")]
    Modified
}
