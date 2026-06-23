using System.Net;
using System.Net.Http.Json;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ExamCatalogService.Infrastructure.Clients;

public class AiGradingClient : IAiGradingClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiGradingClient> _logger;

    public AiGradingClient(HttpClient httpClient, ILogger<AiGradingClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<ExtractGridResponse?> ExtractRubricAsync(
        string presignedUrl,
        string contentType,
        decimal? subjectMaxScore,
        CancellationToken ct = default)
    {
        try
        {
            var body = new { presignedUrl, contentType, subjectMaxScore };
            var response = await _httpClient.PostAsJsonAsync("/ai/rubric/extract", body, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogWarning(
                    "AIGradingService rejected request: internal API key mismatch (status {StatusCode}). " +
                    "Ensure INTERNAL_API_KEY matches between ExamCatalog and the AI service.",
                    response.StatusCode);
                return null;
            }

            if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
            {
                _logger.LogWarning("AIGradingService unavailable (status {StatusCode})", response.StatusCode);
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ExtractGridResponse>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "AIGradingService unreachable for rubric extraction");
            return null;
        }
    }
}
