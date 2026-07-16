using Amazon.S3;
using Amazon.S3.Model;
using BuildingBlocks.AwsS3;
using Microsoft.Extensions.Options;
using SubmissionService.Application.Interfaces;

namespace SubmissionService.Infrastructure.Services;

public class S3Service : IS3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly AwsS3Settings _settings;

    public S3Service(IAmazonS3 s3Client, IOptions<AwsS3Settings> settings)
    {
        _s3Client = s3Client;
        _settings = settings.Value;
    }

    public async Task<string> UploadAsync(string key, Stream stream, string contentType,
        CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(request, ct);
        return key;
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var request = new GetObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = key
        };

        var response = await _s3Client.GetObjectAsync(request, ct);
        return response.ResponseStream;
    }

    public Task<string> GeneratePresignedGetUrlAsync(string s3Key, TimeSpan ttl,
        CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = s3Key,
            Expires = DateTime.UtcNow.Add(ttl),
            Verb = HttpVerb.GET
        };

        return Task.FromResult(_s3Client.GetPreSignedURL(request));
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var request = new DeleteObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = key
        };

        await _s3Client.DeleteObjectAsync(request, ct);
    }
}
