using Contracts.Messages;
using ExamCatalogService.Domain.Entities;
using ExamCatalogService.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExamCatalogService.Infrastructure.Consumers;

/// <summary>
/// Keeps the local marker_assignment_view projection in sync with GradingService's Allocation module.
/// Upsert/delete are keyed on the source assignment id, so processing the same event twice (redelivery)
/// is a no-op — idempotent by design (N8), no inbox table required.
/// </summary>
public class MarkerAssignmentChangedConsumer : IConsumer<MarkerAssignmentChanged>
{
    private readonly ExamCatalogDbContext _db;
    private readonly ILogger<MarkerAssignmentChangedConsumer> _logger;

    public MarkerAssignmentChangedConsumer(
        ExamCatalogDbContext db,
        ILogger<MarkerAssignmentChangedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<MarkerAssignmentChanged> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        if (msg.ChangeType == MarkerAssignmentChangeType.Deleted)
        {
            await _db.MarkerAssignmentViews
                .Where(v => v.Id == msg.AssignmentId)
                .ExecuteDeleteAsync(ct);

            _logger.LogInformation(
                "MarkerAssignmentView deleted: assignment={AssignmentId}", msg.AssignmentId);
            return;
        }

        var existing = await _db.MarkerAssignmentViews
            .FirstOrDefaultAsync(v => v.Id == msg.AssignmentId, ct);

        if (existing is null)
        {
            _db.MarkerAssignmentViews.Add(new MarkerAssignmentView
            {
                Id = msg.AssignmentId,
                SubjectId = msg.SubjectId,
                TeacherId = msg.TeacherId,
                AliasStart = msg.AliasStart,
                AliasEnd = msg.AliasEnd,
                UpdatedAt = msg.OccurredAt
            });
        }
        else
        {
            // Out-of-order guard: a redelivered/stale event can arrive after a newer one. Only apply
            // when this event is strictly newer than what we already persisted, so a late-arriving
            // older update cannot clobber the current state.
            if (msg.OccurredAt <= existing.UpdatedAt)
            {
                _logger.LogInformation(
                    "MarkerAssignmentView skipped (stale): assignment={AssignmentId} " +
                    "occurredAt={OccurredAt} <= existing={ExistingUpdatedAt}",
                    msg.AssignmentId, msg.OccurredAt, existing.UpdatedAt);
                return;
            }

            existing.SubjectId = msg.SubjectId;
            existing.TeacherId = msg.TeacherId;
            existing.AliasStart = msg.AliasStart;
            existing.AliasEnd = msg.AliasEnd;
            existing.UpdatedAt = msg.OccurredAt;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "MarkerAssignmentView upserted: assignment={AssignmentId} subject={SubjectId} teacher={TeacherId}",
            msg.AssignmentId, msg.SubjectId, msg.TeacherId);
    }
}
