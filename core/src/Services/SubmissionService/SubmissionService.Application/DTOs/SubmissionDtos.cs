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
