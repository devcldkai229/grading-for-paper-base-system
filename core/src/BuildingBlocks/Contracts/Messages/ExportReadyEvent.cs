namespace Contracts.Messages;

/// <summary>
/// Published when a subject's grade export finishes generating. Consumed by NotificationService
/// to let the requester know the export is ready.
/// </summary>
public record ExportReadyEvent(
    Guid MessageId,
    Guid RequestedBy,
    Guid SubjectId,
    DateTime OccurredAt
);
