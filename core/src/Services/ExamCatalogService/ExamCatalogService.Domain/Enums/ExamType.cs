using NpgsqlTypes;

namespace ExamCatalogService.Domain.Enums;

public enum ExamType
{
    [PgName("FE")]
    FE,

    [PgName("PE")]
    PE,

    [PgName("PT")]
    PT,

    [PgName("OTHER")]
    Other
}
