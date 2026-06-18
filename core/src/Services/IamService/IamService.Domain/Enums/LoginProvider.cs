using NpgsqlTypes;

namespace IamService.Domain.Enums;

public enum LoginProvider
{
    [PgName("password_auth")]
    PasswordAuth = 0,

    [PgName("google")]
    Google = 1,
}
