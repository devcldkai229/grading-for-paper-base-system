using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using GradingService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GradingService.Infrastructure.Clients;

public class IamServiceClient : IIamServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IamServiceClient> _logger;

    public IamServiceClient(HttpClient httpClient, ILogger<IamServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Dictionary<Guid, string>> GetAllLecturerMarkerCodesAsync(CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, string>();
        try
        {
            var response = await _httpClient.GetAsync("/api/internal/users?pageSize=1000", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return result;
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<Envelope<PagedResult<UserClientDto>>>(JsonOptions, ct);
            if (payload?.Data?.Items != null)
            {
                foreach (var user in payload.Data.Items)
                {
                    if (!string.IsNullOrEmpty(user.MarkerCode))
                    {
                        result[user.Id] = user.MarkerCode;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "IamService unreachable for retrieving lecturer marker codes");
        }

        return result;
    }

    private sealed class Envelope<T>
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    private sealed class PagedResult<T>
    {
        [JsonPropertyName("items")]
        public IEnumerable<T>? Items { get; set; }
    }

    private sealed class UserClientDto
    {
        public Guid Id { get; set; }
        public string? MarkerCode { get; set; }
    }
}
