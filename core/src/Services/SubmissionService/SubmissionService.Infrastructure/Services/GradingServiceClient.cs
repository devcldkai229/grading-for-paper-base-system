using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using SubmissionService.Application.Interfaces;

namespace SubmissionService.Infrastructure.Services;

public class GradingServiceClient : IGradingServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GradingServiceClient> _logger;

    public GradingServiceClient(HttpClient httpClient, ILogger<GradingServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AliasRangeDto>?> GetAliasRangesAsync(
        Guid subjectId, Guid lecturerId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/assignments/subjects/{subjectId}/lecturers/{lecturerId}/alias-ranges", ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Endpoint answered: no assignment.
                return Array.Empty<AliasRangeDto>();
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AliasRangesResponse>(cancellationToken: ct);
            if (result?.Data is null)
            {
                return Array.Empty<AliasRangeDto>();
            }

            return result.Data
                .Select(d => new AliasRangeDto(d.AliasStart, d.AliasEnd))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex,
                "GradingService unreachable for Subject {SubjectId}, Lecturer {LecturerId} — failing closed.",
                subjectId, lecturerId);
            return null; // Fail-closed: caller denies access when null.
        }
    }

    private sealed class AliasRangesResponse
    {
        public List<AliasRangeData>? Data { get; set; }
    }

    private sealed class AliasRangeData
    {
        public int AliasStart { get; set; }
        public int AliasEnd { get; set; }
    }
}
