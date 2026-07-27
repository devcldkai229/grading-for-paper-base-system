using Amazon.S3;
using Amazon.S3.Model;
using BuildingBlocks.AwsS3;
using ExamCatalogService.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace ExamCatalogService.Infrastructure.Services;

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
            ContentType = contentType,
            AutoCloseStream = false
        };

        await _s3Client.PutObjectAsync(request, ct);
        return key;
    }

    public Task<string> GeneratePresignedGetUrlAsync(
        string s3Key,
        TimeSpan ttl,
        string? dispositionFileName = null,
        bool inline = true,
        CancellationToken ct = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _settings.BucketName,
            Key = s3Key,
            Expires = DateTime.UtcNow.Add(ttl),
            Verb = HttpVerb.GET
        };

        if (!string.IsNullOrWhiteSpace(dispositionFileName))
        {
            var disposition = inline ? "inline" : "attachment";
            request.ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = $"{disposition}; filename=\"{dispositionFileName}\""
            };
        }

        return Task.FromResult(_s3Client.GetPreSignedURL(request));
    }
}
