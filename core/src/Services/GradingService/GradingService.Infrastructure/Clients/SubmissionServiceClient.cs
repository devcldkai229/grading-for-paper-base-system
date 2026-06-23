using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GradingService.Infrastructure.Clients;

public class SubmissionServiceClient : ISubmissionServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SubmissionServiceClient> _logger;

    public SubmissionServiceClient(HttpClient httpClient, ILogger<SubmissionServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<BatchPapersClientDto?> GetBatchPapersAsync(Guid batchId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/internal/batches/{batchId}/papers", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<Envelope<BatchPapersClientDto>>(JsonOptions, ct);
            return payload?.Data;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "SubmissionService unreachable for batch {BatchId}", batchId);
            return null;
        }
    }

    public async Task<InternalPaperSummaryClientDto?> GetPaperSummaryAsync(Guid paperId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/internal/papers/{paperId}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<Envelope<InternalPaperSummaryClientDto>>(JsonOptions, ct);
            return payload?.Data;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "SubmissionService unreachable for paper {PaperId}", paperId);
            return null;
        }
    }

    private sealed class Envelope<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }
}
