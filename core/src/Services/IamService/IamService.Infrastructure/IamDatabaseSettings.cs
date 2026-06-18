namespace IamService.Infrastructure;

internal sealed class IamDatabaseSettings
{
    public IamDatabaseSettings(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }
}
