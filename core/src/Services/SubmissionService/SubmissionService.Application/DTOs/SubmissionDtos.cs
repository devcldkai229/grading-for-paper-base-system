namespace SubmissionService.Application.DTOs;

public record BatchDto(
    Guid Id,
    Guid SubjectId,
    string ZipS3Key,
    string? ZipFileName,
    int TotalPapers,
    string Status,
    Guid UploadedBy,
    string? ErrorMessage,
    DateTime CreatedAt
);

public record BatchStatusDto(
    Guid Id,
    string Status,
    int TotalPapers,
    string? ErrorMessage,
    IReadOnlyList<DuplicateFileWarningDto> DuplicateWarnings
);

public record DuplicateFileEntryDto(
    Guid PaperId,
    string? StudentAlias,
    string? FileName
);

/// <summary>Files sharing the same content hash within a batch. Informational only — never blocks upload.</summary>
public record DuplicateFileWarningDto(
    string ContentHash,
    IReadOnlyList<DuplicateFileEntryDto> Files
);

public record StudentPaperDto(
    Guid Id,
    Guid BatchId,
    Guid SubjectId,
    string? StudentAlias,
    int? AliasNumber,
    string Status,
    int FileCount,
    DateTime CreatedAt
);

public record PaperFileDto(
    Guid Id,
    string? FileName,
    string ContentType,
    long? SizeBytes,
    int OrderIndex
);

public record StudentPaperDetailDto(
    Guid Id,
    Guid BatchId,
    Guid SubjectId,
    string? StudentAlias,
    int? AliasNumber,
    string Status,
    IReadOnlyList<PaperFileDto> Files,
    DateTime CreatedAt
);

public record FileUrlResponse(
    string Url,
    string ContentType,
    string FileName
);

public record BatchPaperDto(
    Guid PaperId,
    Guid SubjectId,
    int? AliasNumber
);

public record BatchPapersDto(
    Guid BatchId,
    Guid SubjectId,
    Guid UploadedBy,
    IReadOnlyList<BatchPaperDto> Papers
);

/// <summary>Batch header (existence + ownership) without materializing its papers — used by the
/// gRPC streaming path to emit a header frame before streaming papers one at a time.</summary>
public record BatchSummaryDto(
    Guid BatchId,
    Guid SubjectId,
    Guid UploadedBy
);

public record InternalPaperSummaryDto(
    Guid Id,
    Guid BatchId,
    Guid SubjectId,
    string? StudentAlias,
    int? AliasNumber,
    Guid UploadedBy
);

public record CreateFileBatchResultDto(
    Guid BatchId,
    Guid PaperId
);

public record PaperFileRefDto(
    Guid FileId,
    string S3Key
);

/// <summary>Everything needed to authorize and perform a paper deletion (files to purge from S3 included).</summary>
public record PaperDeletionInfoDto(
    Guid PaperId,
    Guid BatchId,
    Guid SubjectId,
    Guid UploadedBy,
    string Status,
    IReadOnlyList<PaperFileRefDto> Files
);

/// <summary>One audit entry (batch/paper deletion, etc.). Consumed by ReportingService's global audit log viewer.</summary>
public record SubmissionAuditLogEntryDto(
    Guid Id,
    string Action,
    string EntityType,
    Guid EntityId,
    Guid SubjectId,
    Guid PerformedBy,
    string? Details,
    DateTime PerformedAt
);

public record SubmissionAuditLogPageDto(
    IReadOnlyList<SubmissionAuditLogEntryDto> Items,
    int TotalCount
);

/// <summary>Aggregate paper stats for a subject. Consumed by GradingService to validate that an
/// admin's marker-assignment alias range doesn't exceed the papers actually submitted.
/// MaxAliasNumber is null when TotalPapers is 0 (nothing submitted yet).</summary>
public record SubjectPaperStatsDto(int TotalPapers, int? MaxAliasNumber);
