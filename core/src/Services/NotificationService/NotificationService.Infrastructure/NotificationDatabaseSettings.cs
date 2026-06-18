namespace NotificationService.Infrastructure;

internal sealed class NotificationDatabaseSettings
{
    public NotificationDatabaseSettings(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }
}
