using SubmissionService.Domain.Enums;

namespace SubmissionService.Domain.Entities;

public sealed class SubmissionBatch
{
    public Guid Id { get; set; }

    public Guid SubjectId { get; set; }

    public string ZipS3Key { get; set; } = string.Empty;

    public string? ZipFileName { get; set; }

    public int TotalPapers { get; set; }

    public BatchStatus Status { get; set; } = BatchStatus.Uploaded;

    public Guid UploadedBy { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Content-hash collisions found among files ingested in this batch. Informational only — never blocks upload.</summary>
    public List<DuplicateFileWarning> DuplicateWarnings { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public sealed class DuplicateFileWarning
{
    public string ContentHash { get; set; } = string.Empty;

    public List<DuplicateFileEntry> Files { get; set; } = new();
}

public sealed class DuplicateFileEntry
{
    public Guid StudentPaperId { get; set; }

    public string? StudentAlias { get; set; }

    public string? FileName { get; set; }
}
