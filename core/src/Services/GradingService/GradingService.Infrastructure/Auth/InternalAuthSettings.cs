namespace GradingService.Infrastructure.Auth;

public class InternalAuthSettings
{
    public const string SectionName = "InternalAuth";

    public string ApiKey { get; set; } = string.Empty;
}
