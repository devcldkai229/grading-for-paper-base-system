using IamService.Domain.Enums;
using Npgsql;

namespace IamService.Infrastructure.Persistence;

internal static class NpgsqlIamConfiguration
{
    public static void MapEnums(NpgsqlDataSourceBuilder builder)
    {
        builder.MapEnum<LoginProvider>("login_provider");
        builder.MapEnum<UserStatus>("user_status");
        builder.MapEnum<UserRole>("user_role");
    }

    public static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        MapEnums(builder);
        return builder.Build();
    }
}
