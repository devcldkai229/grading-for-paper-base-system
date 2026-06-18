using NpgsqlTypes;

namespace GradingService.Domain.Enums;

public enum AssignmentType
{
    [PgName("first_grade")]
    FirstGrade,

    [PgName("cross_grade")]
    CrossGrade,

    [PgName("re_grade")]
    ReGrade
}
