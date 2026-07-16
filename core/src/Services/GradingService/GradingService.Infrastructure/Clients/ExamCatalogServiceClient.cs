using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GradingService.Infrastructure.Clients;

public class ExamCatalogServiceClient : IExamCatalogServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExamCatalogServiceClient> _logger;

    public ExamCatalogServiceClient(HttpClient httpClient, ILogger<ExamCatalogServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SubjectGradingGridClientDto?> GetGradingGridAsync(Guid subjectId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/internal/subjects/{subjectId}/grading-grid", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<Envelope<SubjectGradingGridClientDto>>(JsonOptions, ct);
            return payload?.Data;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "ExamCatalogService unreachable for subject {SubjectId}", subjectId);
            return null;
        }
    }

    public async Task<SubjectExamInfoClientDto?> GetExamInfoAsync(Guid subjectId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/internal/subjects/{subjectId}/exam-info", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<Envelope<SubjectExamInfoClientDto>>(JsonOptions, ct);
            return payload?.Data;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "ExamCatalogService unreachable for subject {SubjectId} exam-info", subjectId);
            return null;
        }
    }

    private sealed class Envelope<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }
}
