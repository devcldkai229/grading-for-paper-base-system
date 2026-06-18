using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using SubmissionService.Infrastructure.Persistence.Bson;

namespace SubmissionService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSubmissionInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        SubmissionBsonSerialization.RegisterSerializers();

        services.Configure<SubmissionDatabaseSettings>(
            configuration.GetSection(SubmissionDatabaseSettings.SectionName));

        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<SubmissionDatabaseSettings>>().Value;

            if (string.IsNullOrWhiteSpace(settings.ConnectionString))
            {
                throw new InvalidOperationException(
                    $"{SubmissionDatabaseSettings.SectionName}:ConnectionString is missing.");
            }

            return new MongoClient(settings.ConnectionString);
        });

        services.AddScoped(serviceProvider =>
        {
            var client = serviceProvider.GetRequiredService<IMongoClient>();
            var settings = serviceProvider.GetRequiredService<IOptions<SubmissionDatabaseSettings>>().Value;

            if (string.IsNullOrWhiteSpace(settings.DatabaseName))
            {
                throw new InvalidOperationException(
                    $"{SubmissionDatabaseSettings.SectionName}:DatabaseName is missing.");
            }

            return client.GetDatabase(settings.DatabaseName);
        });

        return services;
    }

    public static async Task VerifySubmissionDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<SubmissionDatabaseSettings>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IMongoDatabase>>();

        var command = new BsonDocument("ping", 1);
        await database.RunCommandAsync<BsonDocument>(command);

        logger.LogInformation(
            "MongoDB connection verified for database {DatabaseName} at {ConnectionString}.",
            settings.DatabaseName,
            settings.ConnectionString);
    }
}
