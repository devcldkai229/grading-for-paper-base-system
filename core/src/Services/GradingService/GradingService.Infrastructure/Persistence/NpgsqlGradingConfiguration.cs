using GradingService.Domain.Enums;
using Npgsql;

namespace GradingService.Infrastructure.Persistence;

internal static class NpgsqlGradingConfiguration
{
    public static void MapEnums(NpgsqlDataSourceBuilder builder)
    {
        builder.MapEnum<AssignmentType>("assignment_type");
        builder.MapEnum<GradingProgressStatus>("grading_progress_status");
        builder.MapEnum<AiSyncStatus>("ai_sync_status");
        builder.MapEnum<AiReviewStatus>("ai_review_status");
    }

    public static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        MapEnums(builder);
        return builder.Build();
    }
}
