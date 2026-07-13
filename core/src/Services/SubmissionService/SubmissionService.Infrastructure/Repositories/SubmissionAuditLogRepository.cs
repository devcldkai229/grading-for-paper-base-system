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
}
