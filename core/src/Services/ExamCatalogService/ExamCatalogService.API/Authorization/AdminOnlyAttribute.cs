using Microsoft.AspNetCore.Authorization;

namespace ExamCatalogService.API.Authorization;

/// <summary>
/// Restricts access to users with JWT claim Role == Admin.
/// </summary>
public sealed class AdminOnlyAttribute : AuthorizeAttribute
{
    public const string PolicyName = "AdminOnly";

    public AdminOnlyAttribute() : base(PolicyName)
    {
    }
}
