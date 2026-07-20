using Contracts.Messages;
using GradingService.Application.Interfaces;
using GradingService.Domain.Enums;
using GradingService.Infrastructure.Redis;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace GradingService.Infrastructure.Consumers;

/// <summary>
/// Processes AI grading requests from the queue — sets Processing, calls AIGradingService,
/// marks Failed on error (write-back sets Completed on success).
/// </summary>
public class AiGradeConsumer : IConsumer<AiGradeRequestedEvent>
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(5);

    private readonly IGradingAssignmentRepository _assignments;
    private readonly IUnitOfWork _uow;
    private readonly IAiGradingServiceClient _aiClient;
    private readonly IAiGradeLockService _lockService;
    private readonly ILogger<AiGradeConsumer> _logger;

    public AiGradeConsumer(
        IGradingAssignmentRepository assignments,
        IUnitOfWork uow,
        IAiGradingServiceClient aiClient,
        IAiGradeLockService lockService,
        ILogger<AiGradeConsumer> logger)
    {
        _assignments = assignments;
        _uow = uow;
        _aiClient = aiClient;
        _lockService = lockService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AiGradeRequestedEvent> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        _logger.LogInformation(
            "AI grade job started: assignment={AssignmentId} message={MessageId}",
            msg.AssignmentId, msg.MessageId);

        var assignment = await _assignments.GetWithFormAsync(msg.AssignmentId, msg.TeacherId, asNoTracking: false, ct);
        if (assignment?.GradingForm is null)
        {
            _logger.LogWarning("AI grade job skipped — assignment {AssignmentId} not found", msg.AssignmentId);
            return;
        }

        if (assignment.Status == GradingProgressStatus.Submitted)
        {
            _logger.LogInformation("AI grade job skipped — assignment {AssignmentId} already submitted", msg.AssignmentId);
            return;
        }

        if (assignment.AiStatus is not (AiSyncStatus.Queued or AiSyncStatus.Processing))
        {
            _logger.LogInformation(
                "AI grade job skipped — assignment {AssignmentId} status={Status}",
                msg.AssignmentId, assignment.AiStatus);
            return;
        }

        if (!await _lockService.TryAcquireAsync(msg.AssignmentId, LockTtl, ct))
        {
            _logger.LogWarning("AI grade job skipped — lock held for assignment {AssignmentId}", msg.AssignmentId);
            return;
        }

        try
        {
            assignment.AiStatus = AiSyncStatus.Processing;
            await _uow.SaveChangesAsync(ct);

            var request = MapToClientRequest(msg.Request);
            var ok = await _aiClient.RequestSuggestionsAsync(request, ct);

            if (!ok)
            {
                assignment.AiStatus = AiSyncStatus.Failed;
                await _uow.SaveChangesAsync(ct);
                _logger.LogWarning("AI grade job failed for assignment {AssignmentId}", msg.AssignmentId);
            }
            else
            {
                if (assignment.AiStatus == AiSyncStatus.Processing)
                {
                    assignment.AiStatus = AiSyncStatus.Completed;
                    await _uow.SaveChangesAsync(ct);
                }
                _logger.LogInformation("AI grade job completed for assignment {AssignmentId}", msg.AssignmentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI grade job error for assignment {AssignmentId}", msg.AssignmentId);
            try
            {
                assignment.AiStatus = AiSyncStatus.Failed;
                await _uow.SaveChangesAsync(ct);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to mark assignment {AssignmentId} as Failed", msg.AssignmentId);
            }

            throw;
        }
        finally
        {
            await _lockService.ReleaseAsync(msg.AssignmentId, ct);
        }
    }

    private static Application.DTOs.AiGradeSuggestRequest MapToClientRequest(AiGradeSuggestPayload payload) =>
        new(
            payload.AssignmentId,
            payload.SubjectId,
            payload.RubricVersion,
            payload.Files.Select(f => new Application.DTOs.AiGradeFileRef(f.Url, f.ContentType)).ToList(),
            payload.ScoreGrid.Select(q => new Application.DTOs.AiGradeScoreGridItem(
                q.QuestionNumber, q.GroupLabel, q.Label, q.MaxScore)).ToList(),
            payload.RubricText);
}
