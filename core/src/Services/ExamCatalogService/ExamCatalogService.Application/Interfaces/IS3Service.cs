namespace ExamCatalogService.Application.Interfaces;

public interface IS3Service
{
    /// <summary>
    /// Upload a file to S3 and return the key.
    /// </summary>
    Task<string> UploadAsync(string key, Stream stream, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Generate a pre-signed GET URL for the given S3 key.
    /// </summary>
    Task<string> GeneratePresignedGetUrlAsync(
        string s3Key,
        TimeSpan ttl,
        string? dispositionFileName = null,
        bool inline = true,
        CancellationToken ct = default);
}
