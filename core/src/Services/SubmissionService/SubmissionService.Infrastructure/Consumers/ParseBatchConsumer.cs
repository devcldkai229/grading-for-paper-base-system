using System.IO.Compression;
using System.Security.Cryptography;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StackExchange.Redis;
using SubmissionService.Application;
using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;
using SubmissionService.Domain.Enums;
using SubmissionService.Domain.Messages;
using SubmissionService.Infrastructure.Persistence.Bson;
using SubmissionService.Infrastructure.Persistence.Documents;

namespace SubmissionService.Infrastructure.Consumers;

public class ZipLimits
{
    public const string SectionName = "ZipLimits";
    public int MaxEntries { get; set; } = 500;
    public long MaxTotalBytes { get; set; } = 500 * 1024 * 1024; // 500 MB
    /// <summary>ZIP <= this size is buffered in memory; larger is staged to a temp file.</summary>
    public long MemoryBufferThresholdBytes { get; set; } = 64 * 1024 * 1024; // 64 MB
}

public enum GroupingStrategy
{
    /// <summary>One student = one top-level folder in the ZIP (default).</summary>
    PerTopFolder,
    /// <summary>One student = one file (flat ZIPs).</summary>
    PerFile
}

public class IngestionOptions
{
    public const string SectionName = "Ingestion";
    public GroupingStrategy GroupingStrategy { get; set; } = GroupingStrategy.PerTopFolder;
}

public class ParseBatchConsumer : IConsumer<ParseBatchJob>
{
    private readonly IMongoDatabase _database;
    private readonly IS3Service _s3Service;
    private readonly IBatchRepository _batchRepository;
    private readonly ILogger<ParseBatchConsumer> _logger;
    private readonly ZipLimits _zipLimits;
    private readonly IngestionOptions _ingestion;
    private readonly IConnectionMultiplexer? _redis;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png", "image/jpeg", "image/gif", "image/bmp", "image/tiff", "image/webp",
        "text/plain",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    public ParseBatchConsumer(
        IMongoDatabase database,
        IS3Service s3Service,
        IBatchRepository batchRepository,
        ILogger<ParseBatchConsumer> logger,
        IOptions<ZipLimits> zipLimits,
        IOptions<IngestionOptions> ingestion,
        IConnectionMultiplexer? redis = null)
    {
        _database = database;
        _s3Service = s3Service;
        _batchRepository = batchRepository;
        _logger = logger;
        _zipLimits = zipLimits.Value;
        _ingestion = ingestion.Value;
        _redis = redis;
    }

    public async Task Consume(ConsumeContext<ParseBatchJob> context)
    {
        var job = context.Message;
        var ct = context.CancellationToken;
        _logger.LogInformation("ParseBatchJob received: BatchId={BatchId}, SubjectId={SubjectId}",
            job.BatchId, job.SubjectId);

        // Idempotency: claim the message-id. Released in catch so retries can re-run.
        var redisDb = _redis?.GetDatabase();
        var dedupeKey = $"batch_job:{job.MessageId}";
        if (redisDb is not null)
        {
            var isNew = await redisDb.StringSetAsync(dedupeKey, "1", TimeSpan.FromHours(24), When.NotExists);
            if (!isNew)
            {
                _logger.LogInformation("ParseBatchJob skipped (duplicate): MessageId={MessageId}", job.MessageId);
                return;
            }
        }

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"batch_{job.BatchId}_{Guid.NewGuid():N}.zip");

        try
        {
            await _batchRepository.UpdateBatchStatusAsync(job.BatchId, BatchStatus.Extracting, ct: ct);

            // Blocker fix: S3 GetObject stream is NOT seekable, but ZipArchive(Read) needs seek.
            // Stage the ZIP to a seekable source first (temp file on disk -> handles large ZIPs).
            await using (var s3Stream = await _s3Service.DownloadAsync(job.ZipS3Key, ct))
            await using (var tmpWrite = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await s3Stream.CopyToAsync(tmpWrite, ct);
            }

            int totalPapers;
            IReadOnlyList<DuplicateFileWarningDto> duplicateWarnings;
            await using (var zipFs = new FileStream(tempZipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(zipFs, ZipArchiveMode.Read))
            {
                (totalPapers, duplicateWarnings) = await ProcessArchiveAsync(archive, job, ct);
            }

            await _batchRepository.UpdateBatchStatusAsync(
                job.BatchId, BatchStatus.Ready, totalPapers, duplicateWarnings: duplicateWarnings, ct: ct);
            _logger.LogInformation("ParseBatchJob completed: BatchId={BatchId}, TotalPapers={TotalPapers}",
                job.BatchId, totalPapers);
            if (duplicateWarnings.Count > 0)
            {
                _logger.LogWarning(
                    "ParseBatchJob found {Count} duplicate content hash(es) in BatchId={BatchId}",
                    duplicateWarnings.Count, job.BatchId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ParseBatchJob failed: BatchId={BatchId}", job.BatchId);

            // Release the dedupe claim so MassTransit retry / redelivery can re-process.
            if (redisDb is not null)
            {
                try { await redisDb.KeyDeleteAsync(dedupeKey); } catch { /* best-effort */ }
            }

            // Record failure even if the job was cancelled.
            await _batchRepository.UpdateBatchStatusAsync(
                job.BatchId, BatchStatus.Failed, errorMessage: ex.Message, ct: CancellationToken.None);
            throw; // Let MassTransit retry / dead-letter.
        }
        finally
        {
            TryDeleteFile(tempZipPath);
        }
    }

    private async Task<(int TotalPapers, IReadOnlyList<DuplicateFileWarningDto> DuplicateWarnings)> ProcessArchiveAsync(
        ZipArchive archive, ParseBatchJob job, CancellationToken ct)
    {
        // Zip-bomb guards (declared sizes from the central directory).
        if (archive.Entries.Count > _zipLimits.MaxEntries)
        {
            throw new InvalidOperationException(
                $"ZIP has {archive.Entries.Count} entries, exceeding limit of {_zipLimits.MaxEntries}.");
        }

        long totalBytes = archive.Entries.Sum(e => e.Length);
        if (totalBytes > _zipLimits.MaxTotalBytes)
        {
            throw new InvalidOperationException(
                $"ZIP uncompressed size ({totalBytes} bytes) exceeds limit of {_zipLimits.MaxTotalBytes} bytes.");
        }

        var fileEntries = archive.Entries
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .Where(e => !IsJunkZipEntry(e.FullName))
            .ToList();

        var strategy = DetectGroupingStrategy(fileEntries);
        _logger.LogInformation(
            "ZIP grouping strategy={Strategy} for {FileCount} entries (BatchId={BatchId})",
            strategy, fileEntries.Count, job.BatchId);

        // Grouping = how a "student paper" is identified inside the ZIP.
        IEnumerable<IGrouping<string, ZipArchiveEntry>> grouped = strategy switch
        {
            GroupingStrategy.PerFile => fileEntries.GroupBy(e => e.FullName),
            _ => fileEntries.GroupBy(e => GetStudentFolder(e.FullName))
        };
        // Deterministic ordering => stable alias numbering across retries.
        var studentGroups = grouped.OrderBy(g => g.Key, StringComparer.Ordinal).ToList();

        var papersCollection = _database.GetCollection<StudentPaper>(MongoCollectionNames.For<StudentPaper>());
        var filesCollection = _database.GetCollection<PaperFile>(MongoCollectionNames.For<PaperFile>());

        int aliasNumber = 1;
        int totalPapers = 0;
        var hashedFiles = new List<HashedFileEntry>();

        foreach (var group in studentGroups)
        {
            // Pre-filter: drop zip-slip paths and disallowed file types.
            var validEntries = new List<(ZipArchiveEntry Entry, string ContentType)>();
            foreach (var entry in group)
            {
                var normalizedName = entry.FullName.Replace('\\', '/');
                if (normalizedName.Contains("..") || Path.IsPathRooted(normalizedName))
                {
                    _logger.LogWarning("Zip-Slip detected, skipping entry: {Entry}", entry.FullName);
                    continue;
                }

                var contentType = GetContentType(entry.Name);
                if (!AllowedContentTypes.Contains(contentType))
                {
                    _logger.LogWarning("Disallowed file type, skipping entry: {Entry} ({ContentType})",
                        entry.FullName, contentType);
                    continue;
                }

                validEntries.Add((entry, contentType));
            }

            if (validEntries.Count == 0)
            {
                _logger.LogWarning("Group '{Group}' has no valid files; skipping.", group.Key);
                continue; // do NOT consume an alias number for empty papers
            }

            var studentAlias = $"Student_{aliasNumber:D4}";

            // Upsert student paper (idempotent by batch_id + alias_number).
            var paperFilter = Builders<StudentPaper>.Filter.Eq(p => p.BatchId, job.BatchId)
                            & Builders<StudentPaper>.Filter.Eq(p => p.AliasNumber, aliasNumber);
            var existingPaper = await papersCollection.Find(paperFilter).FirstOrDefaultAsync(ct);
            var paperId = existingPaper?.Id ?? Guid.NewGuid();

            if (existingPaper is null)
            {
                await papersCollection.InsertOneAsync(new StudentPaper
                {
                    Id = paperId,
                    BatchId = job.BatchId,
                    SubjectId = job.SubjectId,
                    StudentAlias = studentAlias,
                    AliasNumber = aliasNumber,
                    UploadedBy = job.UploadedBy,
                    Status = PaperStatus.ReadyToAssign,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken: ct);
            }

            int fileIndex = 0;
            foreach (var (entry, contentType) in validEntries)
            {
                var s3Key = $"submissions/{job.SubjectId}/{studentAlias}/{entry.Name}";

                // ZipArchiveEntry.Open() is a non-seekable deflate stream; S3 PutObject needs a
                // seekable stream with known length, so buffer this single entry first.
                string contentHash;
                using (var buffer = new MemoryStream())
                {
                    await using (var entryStream = entry.Open())
                    {
                        await entryStream.CopyToAsync(buffer, ct);
                    }
                    buffer.Position = 0;
                    contentHash = Convert.ToHexStringLower(await SHA256.HashDataAsync(buffer, ct));
                    buffer.Position = 0;
                    await _s3Service.UploadAsync(s3Key, buffer, contentType, ct);
                }

                hashedFiles.Add(new HashedFileEntry(contentHash, paperId, studentAlias, entry.Name));

                // Upsert paper file (idempotent by paper + s3 key).
                var fileFilter = Builders<PaperFile>.Filter.Eq(f => f.StudentPaperId, paperId)
                               & Builders<PaperFile>.Filter.Eq(f => f.S3Key, s3Key);
                var existingFile = await filesCollection.Find(fileFilter).FirstOrDefaultAsync(ct);

                if (existingFile is null)
                {
                    await filesCollection.InsertOneAsync(new PaperFile
                    {
                        Id = Guid.NewGuid(),
                        StudentPaperId = paperId,
                        S3Key = s3Key,
                        FileName = entry.Name,
                        ContentType = contentType,
                        SizeBytes = entry.Length,
                        ContentHash = contentHash,
                        OrderIndex = fileIndex,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken: ct);
                }

                fileIndex++;
            }

            aliasNumber++;
            totalPapers++;
        }

        var duplicateWarnings = DuplicateFileDetector.Detect(hashedFiles);

        return (totalPapers, duplicateWarnings);
    }

    /// <summary>
    /// Detect whether each file in the ZIP should map to one student paper.
    /// Handles root-level files (1.txt, 2.txt) and Windows-style "folder zip"
    /// (MyFolder/1.txt, MyFolder/2.txt).
    /// </summary>
    private GroupingStrategy DetectGroupingStrategy(IReadOnlyList<ZipArchiveEntry> fileEntries)
    {
        if (fileEntries.Count == 0)
        {
            return _ingestion.GroupingStrategy;
        }

        var paths = fileEntries
            .Select(e => NormalizeZipPath(e.FullName))
            .ToList();

        // All files at archive root.
        if (paths.All(p => !p.Contains('/')))
        {
            return GroupingStrategy.PerFile;
        }

        // All files are direct children of a single top-level folder (common when zipping a folder).
        var topFolders = paths.Select(GetTopLevelSegment).Distinct(StringComparer.Ordinal).ToList();
        if (topFolders.Count == 1
            && paths.All(p => p.Split('/', StringSplitOptions.RemoveEmptyEntries).Length == 2))
        {
            return GroupingStrategy.PerFile;
        }

        return _ingestion.GroupingStrategy;
    }

    private static string NormalizeZipPath(string fullName) =>
        fullName.Replace('\\', '/').TrimStart('/');

    private static string GetTopLevelSegment(string normalizedPath)
    {
        var slash = normalizedPath.IndexOf('/');
        return slash >= 0 ? normalizedPath[..slash] : normalizedPath;
    }

    private static bool IsJunkZipEntry(string fullName)
    {
        var path = NormalizeZipPath(fullName);
        if (path.StartsWith("__MACOSX/", StringComparison.Ordinal)
            || path.Contains("/__MACOSX/", StringComparison.Ordinal))
        {
            return true;
        }

        var fileName = Path.GetFileName(path);
        return fileName.Equals(".DS_Store", StringComparison.Ordinal)
            || fileName.StartsWith("._", StringComparison.Ordinal);
    }

    /// <summary>
    /// First path segment = student folder. "Student1/page1.pdf" -> "Student1"; root file -> "_root".
    /// </summary>
    private static string GetStudentFolder(string fullName)
    {
        var normalized = NormalizeZipPath(fullName);
        var slashIndex = normalized.IndexOf('/');
        return slashIndex >= 0 ? normalized[..slashIndex] : "_root";
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            ".webp" => "image/webp",
            ".txt" => "text/plain",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream"
        };
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete temp file {Path}", path);
        }
    }
}
