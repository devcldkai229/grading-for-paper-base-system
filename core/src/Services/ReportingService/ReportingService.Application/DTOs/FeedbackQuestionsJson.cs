using System.Text.Json;
using Contracts.Messages;

namespace ReportingService.Application.DTOs;

/// <summary>
/// Shared (de)serialization for the per-question feedback stored in <c>feedback_record.questions_json</c>.
/// The consumer serializes from the score event; the feedback export deserializes it back. Both sides use
/// the SAME options here so the round-trip is stable.
/// </summary>
public static class FeedbackQuestionsJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize(IReadOnlyList<ScoreQuestionPayload> questions) =>
        JsonSerializer.Serialize(
            questions.Select(q => new FeedbackQuestionProjection(
                q.QuestionNumber, q.Label, q.Score, q.MaxScore, q.QuestionComment)),
            Options);

    public static IReadOnlyList<FeedbackQuestionProjection> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<FeedbackQuestionProjection>();
        return JsonSerializer.Deserialize<List<FeedbackQuestionProjection>>(json, Options)
            ?? new List<FeedbackQuestionProjection>();
    }
}
