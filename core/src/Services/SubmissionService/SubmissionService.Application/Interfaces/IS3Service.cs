namespace SubmissionService.Application.Interfaces;

public interface IS3Service
{
    /// <summary>Upload a file stream to S3 and return the object key.</summary>
    Task<string> UploadAsync(string key, Stream stream, string contentType, CancellationToken ct = default);

    /// <summary>Download an object from S3 as a stream.</summary>
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);

    /// <summary>Generate a pre-signed GET URL.</summary>
    Task<string> GeneratePresignedGetUrlAsync(string s3Key, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>Delete an object from S3. No-op (does not throw) if the key does not exist.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);
}
