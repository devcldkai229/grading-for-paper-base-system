using ExamCatalogService.Domain.Enums;
using Npgsql;

namespace ExamCatalogService.Infrastructure.Persistence;

internal static class NpgsqlExamCatalogConfiguration
{
    public static void MapEnums(NpgsqlDataSourceBuilder builder)
    {
        builder.MapEnum<ExamType>("exam_type");
        builder.MapEnum<SubjectStatus>("subject_status");
    }

    public static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        MapEnums(builder);
        return builder.Build();
    }
}
