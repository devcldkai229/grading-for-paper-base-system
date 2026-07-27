using Contracts.Messages;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReportingService.Domain.Entities;
using ReportingService.Infrastructure.Persistence;

namespace ReportingService.Infrastructure.Consumers;

/// <summary>
/// Appends audit-trail entries published by Grading and Iam into the local <c>audit_record</c> projection
/// that backs the admin global audit-log viewer. Append-only and deduplicated on MessageId (belt-and-braces
/// on top of the MassTransit EF inbox).
/// </summary>
public class AuditLogRecordedConsumer : IConsumer<AuditLogRecorded>
{
    private readonly ReportingDbContext _db;
    private readonly ILogger<AuditLogRecordedConsumer> _logger;

    public AuditLogRecordedConsumer(ReportingDbContext db, ILogger<AuditLogRecordedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AuditLogRecorded> context)
    {
        var m = context.Message;
        var ct = context.CancellationToken;

        var alreadyStored = await _db.AuditRecords.AnyAsync(a => a.MessageId == m.MessageId, ct);
        if (alreadyStored)
        {
            _logger.LogDebug("Ignored duplicate AuditLogRecorded {MessageId}", m.MessageId);
            return;
        }

        _db.AuditRecords.Add(new AuditRecord
        {
            MessageId = m.MessageId,
            SourceService = m.SourceService,
            UserId = m.UserId,
            Action = m.Action,
            EntityType = m.EntityType,
            EntityId = m.EntityId,
            OldValue = m.OldValue,
            NewValue = m.NewValue,
            Reason = m.Reason,
            OccurredAt = m.OccurredAt
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Audit record projected: source={Source} action={Action} user={UserId}",
            m.SourceService, m.Action, m.UserId);
    }
}
