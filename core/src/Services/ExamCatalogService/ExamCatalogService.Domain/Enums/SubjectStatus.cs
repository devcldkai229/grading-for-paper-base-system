using NpgsqlTypes;

namespace ExamCatalogService.Domain.Enums;

public enum SubjectStatus
{
    [PgName("draft")]
    Draft,

    [PgName("open")]
    Open,

    [PgName("grading")]
    Grading,

    [PgName("closed")]
    Closed
}
