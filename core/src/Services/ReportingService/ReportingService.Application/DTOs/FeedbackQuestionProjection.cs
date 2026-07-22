namespace ReportingService.Application.DTOs;

/// <summary>
/// Per-question feedback as stored inside <c>feedback_record.questions_json</c>. Shared by the consumer
/// (which serializes it from the ScoreSubmitted/Overridden event) and the feedback export query (which
/// deserializes it back). Serialize/deserialize with the SAME options on both sides.
/// </summary>
public record FeedbackQuestionProjection(
    string QuestionNumber,
    string? Label,
    decimal Score,
    decimal MaxScore,
    string? QuestionComment
);
