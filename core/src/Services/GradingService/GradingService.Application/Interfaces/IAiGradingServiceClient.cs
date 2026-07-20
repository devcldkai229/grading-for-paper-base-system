using GradingService.Application.DTOs;

namespace GradingService.Application.Interfaces;

/// <summary>
/// HTTP client for the AIGradingService Python microservice.
/// Follows the same graceful-degradation convention as ISubmissionServiceClient:
/// unreachable AI service returns false/null, never throws to the caller.
/// </summary>
public interface IAiGradingServiceClient
{
    /// <summary>
    /// Request AI grading suggestions for an assignment (sync mode).
    /// The AI service will call back via POST /api/internal/ai/ai-suggestions when done.
    /// Returns false if the AI service is unreachable (graceful degradation — grading continues manually).
    /// </summary>
    Task<bool> RequestSuggestionsAsync(AiGradeSuggestRequest request, CancellationToken ct = default);
}
