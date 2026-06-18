namespace ReportingService.Infrastructure;

internal sealed class ReportingDatabaseSettings
{
    public ReportingDatabaseSettings(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }
}
