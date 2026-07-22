using Contracts.Messages;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReportingService.Domain.Entities;
using ReportingService.Infrastructure.Persistence;

namespace ReportingService.Infrastructure.Consumers;

/// <summary>
/// Keeps the <c>assignment_progress</c> projection in sync with GradingService's assignment lifecycle,
/// then recomputes the <c>subject_progress</c>/<c>marker_progress</c> read models. Idempotent (MassTransit
/// EF inbox) and out-of-order safe (only newer <c>OccurredAt</c> overwrites the status).
/// </summary>
public class GradingAssignmentStatusChangedConsumer : IConsumer<GradingAssignmentStatusChanged>
{
    private readonly ReportingDbContext _db;
    private readonly ILogger<GradingAssignmentStatusChangedConsumer> _logger;

    public GradingAssignmentStatusChangedConsumer(
        ReportingDbContext db, ILogger<GradingAssignmentStatusChangedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<GradingAssignmentStatusChanged> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var existing = await _db.AssignmentProgress
            .FirstOrDefaultAsync(a => a.AssignmentId == msg.AssignmentId, ct);

        if (existing is null)
        {
            _db.AssignmentProgress.Add(new AssignmentProgress
            {
                AssignmentId = msg.AssignmentId,
                SubjectId = msg.SubjectId,
                TeacherId = msg.TeacherId,
                Status = msg.Status,
                AliasNumber = msg.AliasNumber,
                OccurredAt = msg.OccurredAt,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else if (msg.OccurredAt > existing.OccurredAt)
        {
            existing.SubjectId = msg.SubjectId;
            existing.TeacherId = msg.TeacherId;
            existing.Status = msg.Status;
            existing.AliasNumber = msg.AliasNumber ?? existing.AliasNumber;
            existing.OccurredAt = msg.OccurredAt;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _logger.LogDebug(
                "Ignored out-of-order status for assignment {AssignmentId} (msg {MsgAt:o} <= stored {StoredAt:o})",
                msg.AssignmentId, msg.OccurredAt, existing.OccurredAt);
            return;
        }

        await _db.SaveChangesAsync(ct);

        await ProjectionRecomputer.RecomputeSubjectAsync(_db, msg.SubjectId, ct);
        await ProjectionRecomputer.RecomputeMarkerAsync(_db, msg.SubjectId, msg.TeacherId, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Assignment progress updated: assignment={AssignmentId} subject={SubjectId} status={Status}",
            msg.AssignmentId, msg.SubjectId, msg.Status);
    }
}
