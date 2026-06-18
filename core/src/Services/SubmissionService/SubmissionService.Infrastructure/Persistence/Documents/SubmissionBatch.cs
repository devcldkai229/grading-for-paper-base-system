using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SubmissionService.Domain.Enums;
using SubmissionService.Infrastructure.Persistence.Bson;

namespace SubmissionService.Infrastructure.Persistence.Documents;

[BsonCollection("submission_batches")]
public sealed class SubmissionBatch
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }

    [BsonElement("subject_id")]
    [BsonRepresentation(BsonType.String)]
    public Guid SubjectId { get; set; }

    [BsonElement("zip_s3_key")]
    public string ZipS3Key { get; set; } = string.Empty;

    [BsonElement("zip_file_name")]
    public string? ZipFileName { get; set; }

    [BsonElement("total_papers")]
    public int TotalPapers { get; set; }

    [BsonElement("status")]
    public BatchStatus Status { get; set; } = BatchStatus.Uploaded;

    [BsonElement("uploaded_by")]
    [BsonRepresentation(BsonType.String)]
    public Guid UploadedBy { get; set; }

    [BsonElement("error_message")]
    public string? ErrorMessage { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
