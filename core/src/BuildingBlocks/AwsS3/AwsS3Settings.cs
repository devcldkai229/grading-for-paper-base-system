namespace BuildingBlocks.AwsS3;

public class AwsS3Settings
{
    public const string SectionName = "AwsS3";

    /// <summary>AWS region, e.g. ap-southeast-1. Required when ServiceUrl is not set.</summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>Optional custom S3-compatible endpoint. Leave empty for AWS S3.</summary>
    public string? ServiceUrl { get; set; }

    public string BucketName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Use false for AWS S3 (virtual-hosted style). True only for MinIO/custom endpoints.</summary>
    public bool ForcePathStyle { get; set; }

    public int PresignedUrlTtlMinutes { get; set; } = 10;

    public TimeSpan PresignedUrlTtl => TimeSpan.FromMinutes(PresignedUrlTtlMinutes);
}
