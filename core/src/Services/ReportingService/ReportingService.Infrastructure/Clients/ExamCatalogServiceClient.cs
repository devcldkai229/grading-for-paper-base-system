using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;

namespace ReportingService.Infrastructure.Clients;

public class ExamCatalogServiceClient : IExamCatalogServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExamCatalogServiceClient> _logger;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ExamCatalogServiceClient(HttpClient httpClient, ILogger<ExamCatalogServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SubjectFilterResultClientDto>?> SearchSubjectsAsync(
        Guid? semesterId, Guid? examId, CancellationToken ct = default)
    {
        try
        {
            var query = new List<string>();
            if (semesterId.HasValue) query.Add($"semesterId={semesterId}");
            if (examId.HasValue) query.Add($"examId={examId}");
            var queryString = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;

            var response = await _httpClient.GetAsync($"/api/internal/subjects/search{queryString}", ct);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<Envelope<List<SubjectFilterResultClientDto>>>(JsonOptions, ct);
            return payload?.Data;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "ExamCatalogService unreachable for subject search (semesterId={SemesterId}, examId={ExamId})", semesterId, examId);
            return null;
        }
    }

    private sealed class Envelope<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }
}
