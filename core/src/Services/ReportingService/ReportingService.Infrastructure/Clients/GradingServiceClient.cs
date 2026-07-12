using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;

namespace ReportingService.Infrastructure.Clients;

public class GradingServiceClient : IGradingServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GradingServiceClient> _logger;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GradingServiceClient(HttpClient httpClient, ILogger<GradingServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SubjectFeedbackClientDto?> GetReleasableFeedbackAsync(Guid subjectId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/internal/subjects/{subjectId}/feedback", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<Envelope<SubjectFeedbackClientDto>>(JsonOptions, ct);
            return payload?.Data;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "GradingService unreachable for subject {SubjectId} feedback export", subjectId);
            return null;
        }
    }

    public async Task<GradingProgressDashboardClientDto?> GetProgressDashboardAsync(
        IReadOnlyCollection<Guid>? subjectIds, CancellationToken ct = default)
    {
        try
        {
            var query = subjectIds is { Count: > 0 }
                ? "?" + string.Join("&", subjectIds.Select(id => $"subjectIds={id}"))
                : string.Empty;

            var response = await _httpClient.GetAsync($"/api/internal/grading/progress{query}", ct);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<Envelope<GradingProgressDashboardClientDto>>(JsonOptions, ct);
            return payload?.Data;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "GradingService unreachable for grading progress dashboard");
            return null;
        }
    }

    public async Task<ScoreDistributionDashboardClientDto?> GetScoreDistributionAsync(
        IReadOnlyCollection<Guid>? subjectIds, CancellationToken ct = default)
    {
        try
        {
            var query = subjectIds is { Count: > 0 }
                ? "?" + string.Join("&", subjectIds.Select(id => $"subjectIds={id}"))
                : string.Empty;

            var response = await _httpClient.GetAsync($"/api/internal/grading/score-distribution{query}", ct);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<Envelope<ScoreDistributionDashboardClientDto>>(JsonOptions, ct);
            return payload?.Data;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "GradingService unreachable for score distribution report");
            return null;
        }
    }

    private sealed class Envelope<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }
}
