using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.AspNetCore.Health;

public static class HealthCheckResponseWriter
{
    public static Task WriteMinimalJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var status = report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy";
        return context.Response.WriteAsJsonAsync(new { status, report.TotalDuration });
    }
}
