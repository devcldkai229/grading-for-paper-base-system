using System.Net;
using System.Net.Http.Json;
using ExamCatalogService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ExamCatalogService.Infrastructure.Clients;

public class GradingServiceClient : IGradingServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GradingServiceClient> _logger;

    public GradingServiceClient(HttpClient httpClient, ILogger<GradingServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Guid>?> GetAssignedSubjectIdsAsync(Guid lecturerId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/assignments/lecturers/{lecturerId}/subjects", ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Array.Empty<Guid>();
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AssignedSubjectsResponse>(cancellationToken: ct);
            return (IReadOnlyList<Guid>?)result?.Data ?? Array.Empty<Guid>();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex,
                "GradingService unreachable for Lecturer {LecturerId} — failing closed.", lecturerId);
            return null; // Fail-closed: caller must treat null as "no visible subjects".
        }
    }

    private sealed class AssignedSubjectsResponse
    {
        public List<Guid>? Data { get; set; }
    }
}
