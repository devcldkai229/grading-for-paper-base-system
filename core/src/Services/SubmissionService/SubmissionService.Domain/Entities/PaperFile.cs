namespace SubmissionService.Domain.Entities;

public sealed class PaperFile
{
    public Guid Id { get; set; }

    public Guid StudentPaperId { get; set; }

    public string S3Key { get; set; } = string.Empty;

    public string? FileName { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public long? SizeBytes { get; set; }

    /// <summary>SHA-256 hex digest of the file content, used for duplicate detection within a batch.</summary>
    public string? ContentHash { get; set; }

    public int OrderIndex { get; set; }

    public DateTime CreatedAt { get; set; }
}
