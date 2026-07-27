namespace SubmissionService.Domain.Messages;

/// <summary>
/// Message contract published to RabbitMQ when a ZIP is uploaded.
/// MassTransit consumer picks this up to parse the batch.
/// </summary>
public record ParseBatchJob(
    Guid BatchId,
    Guid SubjectId,
    string ZipS3Key,
    Guid UploadedBy,
    Guid MessageId
);
