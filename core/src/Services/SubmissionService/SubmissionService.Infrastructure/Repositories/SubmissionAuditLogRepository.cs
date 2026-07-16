using MongoDB.Driver;
using SubmissionService.Application.Interfaces;
using SubmissionService.Domain.Entities;
using SubmissionService.Infrastructure.Persistence.Bson;

namespace SubmissionService.Infrastructure.Repositories;

public class SubmissionAuditLogRepository : ISubmissionAuditLogRepository
{
    private readonly IMongoDatabase _database;

    public SubmissionAuditLogRepository(IMongoDatabase database)
    {
        _database = database;
    }

    private IMongoCollection<SubmissionAuditLog> Collection =>
        _database.GetCollection<SubmissionAuditLog>(MongoCollectionNames.For<SubmissionAuditLog>());

    public async Task LogAsync(
        string action, string entityType, Guid entityId, Guid subjectId, Guid performedBy,
        string? details = null, CancellationToken ct = default)
    {
        await Collection.InsertOneAsync(new SubmissionAuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            SubjectId = subjectId,
            PerformedBy = performedBy,
            Details = details,
            PerformedAt = DateTime.UtcNow
        }, cancellationToken: ct);
    }

    public async Task<(IReadOnlyList<SubmissionAuditLog> Items, int TotalCount)> ListAsync(
        Guid? userId, string? entityType, string? action, int page, int pageSize, CancellationToken ct = default)
    {
        var filterBuilder = Builders<SubmissionAuditLog>.Filter;
        var filter = filterBuilder.Empty;

        if (userId.HasValue) filter &= filterBuilder.Eq(l => l.PerformedBy, userId.Value);
        if (!string.IsNullOrWhiteSpace(entityType)) filter &= filterBuilder.Eq(l => l.EntityType, entityType);
        if (!string.IsNullOrWhiteSpace(action)) filter &= filterBuilder.Eq(l => l.Action, action);

        var totalCount = (int)await Collection.CountDocumentsAsync(filter, cancellationToken: ct);

        var items = await Collection
            .Find(filter)
            .SortByDescending(l => l.PerformedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
