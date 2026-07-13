using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using SubmissionService.Domain.Enums;
using SubmissionService.Domain.Messages;

namespace SubmissionService.Application.Services;

public class BatchService : IBatchService
{
    private readonly IBatchRepository _batchRepository;
    private readonly IStudentPaperRepository _paperRepository;
    private readonly IS3Service _s3Service;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ISubmissionAuditLogRepository _auditLogRepository;

    public BatchService(
        IBatchRepository batchRepository,
        IStudentPaperRepository paperRepository,
        IS3Service s3Service,
        IMessagePublisher messagePublisher,
        ISubmissionAuditLogRepository auditLogRepository)
    {
        _batchRepository = batchRepository;
        _paperRepository = paperRepository;
        _s3Service = s3Service;
        _messagePublisher = messagePublisher;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<ServiceResult<Guid>> UploadBatchAsync(
        Guid subjectId, Guid userId, UploadFileInput file, CancellationToken ct = default)
    {
        if (file.Length == 0)
        {
            return ServiceResult<Guid>.Invalid("File is required and must not be empty");
        }

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<Guid>.Invalid("Only .zip files are accepted");
        }

        var batchId = Guid.NewGuid();
        var s3Key = $"submissions/{subjectId}/{batchId}.zip";

        // Upload ZIP to S3
        await _s3Service.UploadAsync(s3Key, file.Content, "application/zip", ct);

        // Create batch in MongoDB
        var batch = await _batchRepository.CreateBatchAsync(subjectId, s3Key, file.FileName, userId, ct);

        // Publish ParseBatchJob. MessageId = batch.Id => stable, so redelivery is deduped.
        await _messagePublisher.PublishAsync(new ParseBatchJob(
            batch.Id, subjectId, s3Key, userId, batch.Id), ct);

        return ServiceResult<Guid>.Ok(batch.Id);
    }

    public async Task<ServiceResult<CreateFileBatchResultDto>> UploadFilesAsync(
        Guid subjectId, Guid userId, IReadOnlyList<UploadFileInput> files, CancellationToken ct = default)
    {
        var validated = new List<(UploadFileInput Input, string ContentType)>();
        foreach (var file in files)
        {
            if (!PaperContentTypes.IsAllowed(file.FileName, out var contentType))
            {
                return ServiceResult<CreateFileBatchResultDto>.Invalid($"File type not allowed: {file.FileName}");
            }

            validated.Add((file, contentType));
        }

        if (validated.Count == 0)
        {
            return ServiceResult<CreateFileBatchResultDto>.Invalid("No valid files to upload");
        }

        var batch = await _batchRepository.CreateFileBatchAsync(subjectId, userId, ct);
        const int aliasNumber = 1;
        var studentAlias = $"Student_{aliasNumber:D4}";

        var uploaded = new List<(string FileName, string ContentType, long SizeBytes, string S3Key)>();
        foreach (var (input, contentType) in validated)
        {
            var s3Key = $"submissions/{subjectId}/{studentAlias}/{input.FileName}";
            await _s3Service.UploadAsync(s3Key, input.Content, contentType, ct);
            uploaded.Add((input.FileName, contentType, input.Length, s3Key));
        }

        var (paperId, _) = await _paperRepository.CreateSinglePaperWithFilesAsync(
            batch.Id, subjectId, userId, aliasNumber, uploaded, ct);

        return ServiceResult<CreateFileBatchResultDto>.Ok(new CreateFileBatchResultDto(batch.Id, paperId));
    }

    public Task<BatchStatusDto?> GetBatchStatusAsync(Guid batchId, CancellationToken ct = default) =>
        _batchRepository.GetBatchStatusAsync(batchId, ct);

    public async Task<ServiceResult<Guid>> RetryBatchAsync(
        Guid batchId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var batch = await _batchRepository.GetBatchAsync(batchId, ct);
        if (batch is null)
        {
            return ServiceResult<Guid>.NotFound();
        }

        if (!isAdmin && userId != batch.UploadedBy)
        {
            return ServiceResult<Guid>.Forbidden();
        }

        var marked = await _batchRepository.TryMarkFailedForRetryAsync(batchId, ct);
        if (!marked)
        {
            return ServiceResult<Guid>.Conflict(
                $"Batch cannot be retried from its current status ({batch.Status}). Only Failed batches can be retried.");
        }

        // Reuse batch.Id as MessageId, matching the original upload publish (BatchesController.UploadBatch):
        // the consumer's Redis dedupe claim for this MessageId was released when the prior run failed,
        // so this republish is safe to process and reuses the same idempotent upsert logic (no duplicate papers).
        await _messagePublisher.PublishAsync(new ParseBatchJob(
            batch.Id, batch.SubjectId, batch.ZipS3Key, batch.UploadedBy, batch.Id), ct);

        return ServiceResult<Guid>.Ok(batch.Id);
    }

    public async Task<ServiceResult<object?>> DeleteBatchAsync(
        Guid batchId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var batch = await _batchRepository.GetBatchAsync(batchId, ct);
        if (batch is null)
        {
            return ServiceResult<object?>.NotFound();
        }

        if (!isAdmin && userId != batch.UploadedBy)
        {
            return ServiceResult<object?>.Forbidden();
        }

        if (batch.Status == BatchStatus.Extracting.ToString())
        {
            return ServiceResult<object?>.Conflict("Batch is still being processed; try again once it finishes.");
        }

        var papers = await _paperRepository.GetPapersForDeletionAsync(batchId, ct);
        if (papers.Any(p => p.Status != PaperStatus.ReadyToAssign.ToString()))
        {
            return ServiceResult<object?>.Conflict("Batch has papers that already started grading; it can no longer be deleted.");
        }

        foreach (var file in papers.SelectMany(p => p.Files))
        {
            await _s3Service.DeleteAsync(file.S3Key, ct);
        }
        if (!string.IsNullOrEmpty(batch.ZipS3Key))
        {
            await _s3Service.DeleteAsync(batch.ZipS3Key, ct);
        }

        await _paperRepository.DeletePapersByBatchAsync(batchId, ct);
        await _batchRepository.DeleteBatchRecordAsync(batchId, ct);

        await _auditLogRepository.LogAsync(
            "DeleteBatch", "Batch", batchId, batch.SubjectId, userId,
            $"Deleted batch '{batch.ZipFileName}' ({papers.Count} paper(s))", ct);

        return ServiceResult<object?>.Ok(null);
    }
}
