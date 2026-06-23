using ExamCatalogService.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace ExamCatalogService.Infrastructure.Middleware;

public class InternalApiKeyMiddleware
{
    public const string HeaderName = "X-Internal-Api-Key";

    private readonly RequestDelegate _next;
    private readonly InternalAuthSettings _settings;

    public InternalApiKeyMiddleware(RequestDelegate next, IOptions<InternalAuthSettings> settings)
    {
        _next = next;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/internal", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = 503,
                message = "Internal authentication is not configured.",
                responsedAt = DateTime.UtcNow
            });
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey)
            || providedKey != _settings.ApiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = 401,
                message = "Invalid or missing internal API key.",
                responsedAt = DateTime.UtcNow
            });
            return;
        }

        await _next(context);
    }
}
