namespace ReportingService.Infrastructure.Clients;

/// <summary>Builds the query string shared by every service's internal audit-logs endpoint.</summary>
internal static class AuditLogQueryBuilder
{
    public static string Build(Guid? userId, string? entityType, string? action, int page, int pageSize)
    {
        var parts = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (userId.HasValue) parts.Add($"userId={userId}");
        if (!string.IsNullOrWhiteSpace(entityType)) parts.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (!string.IsNullOrWhiteSpace(action)) parts.Add($"action={Uri.EscapeDataString(action)}");
        return "?" + string.Join("&", parts);
    }
}
