using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;

namespace ReportingService.Infrastructure.Clients;

public class SubmissionServiceClient : ISubmissionServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SubmissionServiceClient> _logger;

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SubmissionServiceClient(HttpClient httpClient, ILogger<SubmissionServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SubmissionAuditLogPageClientDto?> GetAuditLogsAsync(
        Guid? userId, string? entityType, string? action, int page, int pageSize, CancellationToken ct = default)
    {
        try
        {
            var query = AuditLogQueryBuilder.Build(userId, entityType, action, page, pageSize);
            var response = await _httpClient.GetAsync($"/api/internal/audit-logs{query}", ct);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<Envelope<SubmissionAuditLogPageClientDto>>(JsonOptions, ct);
            return payload?.Data;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "SubmissionService unreachable for audit logs");
            return null;
        }
    }

    private sealed class Envelope<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }
}
