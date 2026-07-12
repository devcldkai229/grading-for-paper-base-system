using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SubmissionService.Infrastructure.Persistence.Bson;

namespace SubmissionService.Infrastructure.Persistence.Documents;

[BsonCollection("paper_files")]
public sealed class PaperFile
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("student_paper_id")]
    [BsonRepresentation(BsonType.String)]
    public Guid StudentPaperId { get; set; }

    [BsonElement("s3_key")]
    public string S3Key { get; set; } = string.Empty;

    [BsonElement("file_name")]
    public string? FileName { get; set; }

    [BsonElement("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [BsonElement("size_bytes")]
    public long? SizeBytes { get; set; }

    /// <summary>SHA-256 hex digest of the file content, used for duplicate detection within a batch.</summary>
    [BsonElement("content_hash")]
    public string? ContentHash { get; set; }

    [BsonElement("order_index")]
    public int OrderIndex { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}
