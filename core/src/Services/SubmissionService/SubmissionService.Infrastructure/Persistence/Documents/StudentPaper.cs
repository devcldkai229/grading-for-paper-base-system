using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SubmissionService.Domain.Enums;
using SubmissionService.Infrastructure.Persistence.Bson;

namespace SubmissionService.Infrastructure.Persistence.Documents;

[BsonCollection("student_papers")]
public sealed class StudentPaper
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("batch_id")]
    [BsonRepresentation(BsonType.String)]
    public Guid BatchId { get; set; }

    [BsonElement("subject_id")]
    [BsonRepresentation(BsonType.String)]
    public Guid SubjectId { get; set; }

    [BsonElement("student_alias")]
    public string? StudentAlias { get; set; }

    [BsonElement("alias_number")]
    public int? AliasNumber { get; set; }

    [BsonElement("status")]
    public PaperStatus Status { get; set; } = PaperStatus.ReadyToAssign;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
