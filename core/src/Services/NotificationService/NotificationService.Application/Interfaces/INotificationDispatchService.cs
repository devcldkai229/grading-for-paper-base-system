using Contracts.Messages;

namespace NotificationService.Application.Interfaces;

/// <summary>
/// Turns a business event (assignment, deadline, export-ready, regrade) into a persisted
/// Notification row, recording Sent or Failed. Called by each event's MassTransit consumer
/// after idempotency has already been checked there.
/// </summary>
public interface INotificationDispatchService
{
    Task HandleAssignmentAsync(AssignmentNotificationEvent evt, CancellationToken ct = default);

    Task HandleDeadlineReminderAsync(DeadlineReminderEvent evt, CancellationToken ct = default);

    Task HandleExportReadyAsync(ExportReadyEvent evt, CancellationToken ct = default);

    Task HandleRegradeAsync(RegradeNotificationEvent evt, CancellationToken ct = default);
}
