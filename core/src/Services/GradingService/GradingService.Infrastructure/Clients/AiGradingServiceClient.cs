using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GradingService.Infrastructure.Clients;

public class AiGradingServiceClient : IAiGradingServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiGradingServiceClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AiGradingServiceClient(HttpClient httpClient, ILogger<AiGradingServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> RequestSuggestionsAsync(AiGradeSuggestRequest request, CancellationToken ct = default)
    {
        try
        {
            var body = new
            {
                assignmentId = request.AssignmentId.ToString(),
                subjectId = request.SubjectId.ToString(),
                rubricVersion = request.RubricVersion,
                files = request.Files.Select(f => new { url = f.Url, contentType = f.ContentType }).ToList(),
                scoreGrid = request.ScoreGrid.Select(q => new
                {
                    questionNumber = q.QuestionNumber,
                    groupLabel = q.GroupLabel,
                    label = q.Label,
                    maxScore = q.MaxScore
                }).ToList(),
                rubricText = request.RubricText,
                mode = "sync"
            };

            var response = await _httpClient.PostAsJsonAsync("/ai/grade/paper", body, JsonOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "AIGradingService returned {Status} for assignment {AssignmentId}: {Detail}",
                    (int)response.StatusCode, request.AssignmentId, detail[..Math.Min(detail.Length, 300)]);
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "AIGradingService unreachable for assignment {AssignmentId}", request.AssignmentId);
            return false;
        }
    }
}
