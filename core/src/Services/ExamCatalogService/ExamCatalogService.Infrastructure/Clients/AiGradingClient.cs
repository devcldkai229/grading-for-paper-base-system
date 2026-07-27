using System.Net;
using System.Net.Http.Json;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ExamCatalogService.Infrastructure.Clients;

/// <summary>
/// Calls two Python services:
/// - AIParseQuestionService — POST /ai/rubric/extract (score-grid OCR)
/// - AIGradingService — POST /ai/ingest/rubric (compiled grading contract)
/// </summary>
public class AiGradingClient : IAiGradingClient
{
    private readonly HttpClient _parseClient;
    private readonly HttpClient _gradingClient;
    private readonly ILogger<AiGradingClient> _logger;

    public AiGradingClient(
        IHttpClientFactory httpClientFactory,
        ILogger<AiGradingClient> logger)
    {
        _parseClient = httpClientFactory.CreateClient("AiParseQuestion");
        _gradingClient = httpClientFactory.CreateClient("AiGrading");
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
            var response = await _parseClient.PostAsJsonAsync("/ai/rubric/extract", body, ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogWarning(
                    "AIParseQuestionService rejected request: internal API key mismatch (status {StatusCode}). " +
                    "Ensure INTERNAL_API_KEY matches between ExamCatalog and the AI service.",
                    response.StatusCode);
                return null;
            }

            if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
            {
                _logger.LogWarning("AIParseQuestionService unavailable (status {StatusCode})", response.StatusCode);
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ExtractGridResponse>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "AIParseQuestionService unreachable for rubric extraction");
            return null;
        }
    }

    public async Task<string?> IngestRubricAsync(IngestRubricRequestDto request, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        // #region agent log
        AgentDebugLog("A,B,D", "AiGradingClient.IngestRubricAsync:entry", new
        {
            subjectId = request.SubjectId.ToString("D"),
            rubricVersion = request.RubricVersion,
            fileCount = request.Files.Count,
            scoreGridCount = request.ScoreGrid.Count,
            inboundCtCancelled = ct.IsCancellationRequested,
            baseAddress = _gradingClient.BaseAddress?.ToString(),
            httpTimeoutSec = _gradingClient.Timeout.TotalSeconds
        });
        // #endregion
        try
        {
            var body = new
            {
                subjectId = request.SubjectId.ToString("D"),
                rubricVersion = request.RubricVersion,
                maxScore = request.MaxScore,
                files = request.Files.Select(f => new
                {
                    url = f.Url,
                    contentType = f.ContentType,
                    kind = f.Kind
                }),
                scoreGrid = request.ScoreGrid.Select(q => new
                {
                    questionNumber = q.QuestionNumber,
                    groupLabel = q.GroupLabel,
                    label = q.Label,
                    maxScore = q.MaxScore,
                    parentQuestion = q.ParentQuestion
                })
            };

            // Ingestion is long-running (minutes). Do NOT bind to the inbound request/gRPC
            // CancellationToken — browsers, gateways, and gRPC deadlines cancel far earlier than
            // the LLM finishes, which surfaces as TaskCanceledException / SocketException 995.
            // HttpClient.Timeout is the only cancel for this call.
            var response = await _gradingClient.PostAsJsonAsync(
                "/ai/ingest/rubric", body, CancellationToken.None);

            // #region agent log
            AgentDebugLog("A,C,E", "AiGradingClient.IngestRubricAsync:response", new
            {
                status = (int)response.StatusCode,
                elapsedMs = sw.ElapsedMilliseconds,
                inboundCtCancelled = ct.IsCancellationRequested
            });
            // #endregion

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogWarning(
                    "AIGradingService rejected ingestion: internal API key mismatch (status {StatusCode}).",
                    response.StatusCode);
                return null;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "AIGradingService returned 404 for /ai/ingest/rubric. " +
                    "Check AiGradingServiceUrl points at AIGradingService (default http://localhost:8081), " +
                    "not AIParseQuestionService (8080).");
                return null;
            }

            if (response.StatusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
            {
                _logger.LogWarning("AIGradingService unavailable for ingestion (status {StatusCode})", response.StatusCode);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync(CancellationToken.None);
                _logger.LogWarning(
                    "AIGradingService ingest failed: {StatusCode} {Body}",
                    (int)response.StatusCode,
                    errBody.Length > 500 ? errBody[..500] : errBody);
                return null;
            }

            return await response.Content.ReadAsStringAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // #region agent log
            AgentDebugLog("A,C,D,E", "AiGradingClient.IngestRubricAsync:cancel_or_http", new
            {
                exType = ex.GetType().FullName,
                message = ex.Message,
                innerType = ex.InnerException?.GetType().FullName,
                innerMessage = ex.InnerException?.Message,
                elapsedMs = sw.ElapsedMilliseconds,
                inboundCtCancelled = ct.IsCancellationRequested,
                isTimeout = ex is TaskCanceledException && !ct.IsCancellationRequested
            });
            // #endregion
            _logger.LogError(ex, "AIGradingService unreachable for rubric ingestion");
            return null;
        }
    }

    // #region agent log
    private static void AgentDebugLog(string hypothesisId, string message, object data)
    {
        try
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                sessionId = "769704",
                hypothesisId,
                location = "AiGradingClient.cs",
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                runId = "ingest-cancel-debug"
            });
            File.AppendAllText(
                @"e:\programming\my_project\grading-for-paper-base-system\debug-769704.log",
                payload + Environment.NewLine);
        }
        catch { /* never break ingest for debug */ }
    }
    // #endregion
}
