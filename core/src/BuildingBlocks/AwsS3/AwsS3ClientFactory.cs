using Amazon;
using Amazon.S3;

namespace BuildingBlocks.AwsS3;

public static class AwsS3ClientFactory
{
    public static void Validate(AwsS3Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.BucketName))
        {
            throw new InvalidOperationException($"{AwsS3Settings.SectionName}:BucketName is required.");
        }

        if (string.IsNullOrWhiteSpace(settings.AccessKey) || string.IsNullOrWhiteSpace(settings.SecretKey))
        {
            throw new InvalidOperationException(
                $"{AwsS3Settings.SectionName}:AccessKey and SecretKey are required.");
        }

        if (string.IsNullOrWhiteSpace(settings.ServiceUrl) && string.IsNullOrWhiteSpace(settings.Region))
        {
            throw new InvalidOperationException(
                $"{AwsS3Settings.SectionName}:Region is required when ServiceUrl is not set.");
        }
    }

    public static IAmazonS3 CreateClient(AwsS3Settings settings)
    {
        Validate(settings);

        if (!string.IsNullOrWhiteSpace(settings.ServiceUrl))
        {
            var config = new AmazonS3Config
            {
                ServiceURL = settings.ServiceUrl,
                ForcePathStyle = settings.ForcePathStyle
            };

            if (!string.IsNullOrWhiteSpace(settings.Region))
            {
                config.AuthenticationRegion = settings.Region;
            }

            return new AmazonS3Client(settings.AccessKey, settings.SecretKey, config);
        }

        var region = RegionEndpoint.GetBySystemName(settings.Region);
        return new AmazonS3Client(settings.AccessKey, settings.SecretKey, region);
    }
}
