using NpgsqlTypes;

namespace IamService.Domain.Enums;

public enum UserRole
{
    [PgName("lecturer")]
    Lecturer = 0,

    [PgName("admin")]
    Admin = 1
}
