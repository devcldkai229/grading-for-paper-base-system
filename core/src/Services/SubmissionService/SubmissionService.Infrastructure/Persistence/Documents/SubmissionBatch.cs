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

    /// <summary>Content-hash collisions found among files ingested in this batch. Informational only — never blocks upload.</summary>
    [BsonElement("duplicate_warnings")]
    public List<DuplicateFileWarning> DuplicateWarnings { get; set; } = new();

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class DuplicateFileWarning
{
    [BsonElement("content_hash")]
    public string ContentHash { get; set; } = string.Empty;

    [BsonElement("files")]
    public List<DuplicateFileEntry> Files { get; set; } = new();
}

public sealed class DuplicateFileEntry
{
    [BsonElement("student_paper_id")]
    [BsonRepresentation(BsonType.String)]
    public Guid StudentPaperId { get; set; }

    [BsonElement("student_alias")]
    public string? StudentAlias { get; set; }

    [BsonElement("file_name")]
    public string? FileName { get; set; }
}
