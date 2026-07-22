using Contracts.Messages;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace GradingService.Infrastructure.Consumers;

/// <summary>
/// Consumes AI grading results published by the Python AIGradingService worker and applies them to
/// the assignment — the SINGLE writer that moves AiStatus to Completed. Idempotent by MessageId
/// (MassTransit EF inbox) plus row-level idempotency inside <see cref="ApplyAiSuggestionsAsync"/>.
/// </summary>
public class AiGradeCompletedConsumer : IConsumer<AiGradeCompletedEvent>
{
    private readonly IGradingSessionService _grading;
    private readonly ILogger<AiGradeCompletedConsumer> _logger;

    public AiGradeCompletedConsumer(
        IGradingSessionService grading,
        ILogger<AiGradeCompletedConsumer> logger)
    {
        _grading = grading;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AiGradeCompletedEvent> context)
    {
        var msg = context.Message;

        _logger.LogInformation(
            "AI grade completed: assignment={AssignmentId} message={MessageId} grades={Count}",
            msg.AssignmentId, msg.MessageId, msg.QuestionGrades.Count);

        var request = new ApplyAiSuggestionsRequest(
            msg.AssignmentId,
            msg.QuestionGrades
                .Select(q => new AiQuestionGrade(
                    q.QuestionNumber, q.Score, q.QuestionComment, q.Confidence, q.IsManualOnly))
                .ToList(),
            msg.ModelUsed,
            msg.PromptVersion,
            msg.PaperComment);

        var (success, error) = await _grading.ApplyAiSuggestionsAsync(request, context.CancellationToken);

        if (!success)
        {
            // Terminal (e.g. assignment deleted) — log and ack; retrying won't help.
            _logger.LogWarning(
                "AI grade completed but could not be applied for assignment {AssignmentId}: {Error}",
                msg.AssignmentId, error);
        }
    }
}
