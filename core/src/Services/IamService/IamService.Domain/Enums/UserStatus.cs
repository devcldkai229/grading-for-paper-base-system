using NpgsqlTypes;

namespace IamService.Domain.Enums;

public enum UserStatus
{
    [PgName("active")]
    Active,

    [PgName("inactive")]
    Inactive,

    [PgName("locked")]
    Locked
}
