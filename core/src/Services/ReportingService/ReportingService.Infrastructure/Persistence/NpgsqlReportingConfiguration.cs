using ReportingService.Domain.Enums;
using Npgsql;

namespace ReportingService.Infrastructure.Persistence;

internal static class NpgsqlReportingConfiguration
{
    public static void MapEnums(NpgsqlDataSourceBuilder builder)
    {
        builder.MapEnum<ExportStatus>("export_status");
    }

    public static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        MapEnums(builder);
        return builder.Build();
    }
}
