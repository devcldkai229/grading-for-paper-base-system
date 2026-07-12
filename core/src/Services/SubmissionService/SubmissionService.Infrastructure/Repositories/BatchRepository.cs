using MongoDB.Driver;
using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using SubmissionService.Domain.Enums;
using SubmissionService.Infrastructure.Persistence.Bson;
using SubmissionService.Infrastructure.Persistence.Documents;

namespace SubmissionService.Infrastructure.Repositories;

public class BatchRepository : IBatchRepository
{
    private readonly IMongoDatabase _database;

    public BatchRepository(IMongoDatabase database)
    {
        _database = database;
    }

    private IMongoCollection<SubmissionBatch> Collection =>
        _database.GetCollection<SubmissionBatch>(MongoCollectionNames.For<SubmissionBatch>());

    public async Task<BatchDto> CreateBatchAsync(Guid subjectId, string zipS3Key,
        string? zipFileName, Guid uploadedBy, CancellationToken ct = default)
    {
        var batch = new SubmissionBatch
        {
            Id = Guid.NewGuid(),
            SubjectId = subjectId,
            ZipS3Key = zipS3Key,
            ZipFileName = zipFileName,
            TotalPapers = 0,
            Status = BatchStatus.Uploaded,
            UploadedBy = uploadedBy,
            CreatedAt = DateTime.UtcNow
        };

        await Collection.InsertOneAsync(batch, cancellationToken: ct);

        return new BatchDto(
            batch.Id, batch.SubjectId, batch.ZipS3Key, batch.ZipFileName, batch.TotalPapers,
            batch.Status.ToString(), batch.UploadedBy, batch.ErrorMessage, batch.CreatedAt);
    }

    public async Task<BatchStatusDto?> GetBatchStatusAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await Collection
            .Find(b => b.Id == batchId)
            .FirstOrDefaultAsync(ct);

        if (batch is null) return null;

        return new BatchStatusDto(batch.Id, batch.Status.ToString(),
            batch.TotalPapers, batch.ErrorMessage);
    }

    public async Task UpdateBatchStatusAsync(Guid batchId, BatchStatus status,
        int? totalPapers = null, string? errorMessage = null, CancellationToken ct = default)
    {
        var updateDef = Builders<SubmissionBatch>.Update
            .Set(b => b.Status, status)
            .Set(b => b.UpdatedAt, DateTime.UtcNow);

        if (totalPapers.HasValue)
            updateDef = updateDef.Set(b => b.TotalPapers, totalPapers.Value);

        if (errorMessage is not null)
            updateDef = updateDef.Set(b => b.ErrorMessage, errorMessage);

        await Collection.UpdateOneAsync(b => b.Id == batchId, updateDef, cancellationToken: ct);
    }

    public async Task<BatchDto?> GetBatchAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await Collection.Find(b => b.Id == batchId).FirstOrDefaultAsync(ct);
        if (batch is null) return null;

        return new BatchDto(
            batch.Id, batch.SubjectId, batch.ZipS3Key, batch.ZipFileName, batch.TotalPapers,
            batch.Status.ToString(), batch.UploadedBy, batch.ErrorMessage, batch.CreatedAt);
    }

    public async Task<bool> TryMarkFailedForRetryAsync(Guid batchId, CancellationToken ct = default)
    {
        var filter = Builders<SubmissionBatch>.Filter.Eq(b => b.Id, batchId)
                   & Builders<SubmissionBatch>.Filter.Eq(b => b.Status, BatchStatus.Failed);

        var updateDef = Builders<SubmissionBatch>.Update
            .Set(b => b.Status, BatchStatus.Uploaded)
            .Set(b => b.ErrorMessage, null)
            .Set(b => b.UpdatedAt, DateTime.UtcNow);

        var result = await Collection.UpdateOneAsync(filter, updateDef, cancellationToken: ct);
        return result.ModifiedCount > 0;
    }

    public async Task<BatchDto> CreateFileBatchAsync(Guid subjectId, Guid uploadedBy, CancellationToken ct = default)
    {
        var batch = new SubmissionBatch
        {
            Id = Guid.NewGuid(),
            SubjectId = subjectId,
            ZipS3Key = string.Empty,
            TotalPapers = 1,
            Status = BatchStatus.Ready,
            UploadedBy = uploadedBy,
            CreatedAt = DateTime.UtcNow
        };

        await Collection.InsertOneAsync(batch, cancellationToken: ct);

        return new BatchDto(
            batch.Id, batch.SubjectId, batch.ZipS3Key, batch.ZipFileName, batch.TotalPapers,
            batch.Status.ToString(), batch.UploadedBy, batch.ErrorMessage, batch.CreatedAt);
    }
}
