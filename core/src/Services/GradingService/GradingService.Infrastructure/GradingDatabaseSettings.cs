namespace GradingService.Infrastructure;

internal sealed class GradingDatabaseSettings
{
    public GradingDatabaseSettings(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }
}
