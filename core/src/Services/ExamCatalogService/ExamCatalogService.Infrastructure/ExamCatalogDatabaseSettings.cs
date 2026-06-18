namespace ExamCatalogService.Infrastructure;

internal sealed class ExamCatalogDatabaseSettings
{
    public ExamCatalogDatabaseSettings(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }
}
