using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.AwsS3;

public static class AwsS3ServiceCollectionExtensions
{
    public static IServiceCollection AddAwsS3Client(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AwsS3Settings>(configuration.GetSection(AwsS3Settings.SectionName));

        var settings = configuration.GetSection(AwsS3Settings.SectionName).Get<AwsS3Settings>()
            ?? throw new InvalidOperationException($"{AwsS3Settings.SectionName} configuration is missing.");

        AwsS3ClientFactory.Validate(settings);

        services.AddSingleton<IAmazonS3>(_ => AwsS3ClientFactory.CreateClient(settings));
        return services;
    }
}
