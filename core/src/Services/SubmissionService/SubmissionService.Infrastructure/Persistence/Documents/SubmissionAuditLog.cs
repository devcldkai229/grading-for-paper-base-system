using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SubmissionService.Infrastructure.Persistence.Bson;

namespace SubmissionService.Infrastructure.Persistence.Documents;

[BsonCollection("submission_audit_logs")]
public sealed class SubmissionAuditLog
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("entity_type")]
    public string EntityType { get; set; } = string.Empty;

    [BsonElement("entity_id")]
    [BsonRepresentation(BsonType.String)]
    public Guid EntityId { get; set; }

    [BsonElement("subject_id")]
    [BsonRepresentation(BsonType.String)]
    public Guid SubjectId { get; set; }

    [BsonElement("performed_by")]
    [BsonRepresentation(BsonType.String)]
    public Guid PerformedBy { get; set; }

    [BsonElement("details")]
    public string? Details { get; set; }

    [BsonElement("performed_at")]
    public DateTime PerformedAt { get; set; }
}
