namespace SubmissionService.Infrastructure;

public sealed class SubmissionDatabaseSettings
{
    public const string SectionName = "SubmissionDatabase";

    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "gradepaper_submission";
}
