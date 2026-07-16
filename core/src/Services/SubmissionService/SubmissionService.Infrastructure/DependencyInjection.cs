using BuildingBlocks.AwsS3;
using MassTransit;
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

        services.Configure<Auth.InternalAuthSettings>(
            configuration.GetSection(Auth.InternalAuthSettings.SectionName));

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

        // Repositories
        services.AddScoped<Application.Interfaces.IBatchRepository, Repositories.BatchRepository>();
        services.AddScoped<Application.Interfaces.IStudentPaperRepository, Repositories.StudentPaperRepository>();
        services.AddScoped<Application.Interfaces.ISubmissionAuditLogRepository, Repositories.SubmissionAuditLogRepository>();

        // S3 (AWS) — required for upload/parse/pre-signed URLs
        services.AddAwsS3Client(configuration);
        services.AddScoped<Application.Interfaces.IS3Service, Services.S3Service>();

        var awsS3Settings = configuration.GetSection(AwsS3Settings.SectionName).Get<AwsS3Settings>()
            ?? new AwsS3Settings();
        services.AddSingleton(new Application.PresignedUrlOptions { Ttl = awsS3Settings.PresignedUrlTtl });

        // Application services
        services.AddScoped<Application.Interfaces.IMessagePublisher, Messaging.MassTransitMessagePublisher>();
        services.AddScoped<Application.Interfaces.IBatchService, Application.Services.BatchService>();
        services.AddScoped<Application.Interfaces.IPaperQueryService, Application.Services.PaperQueryService>();

        // MassTransit + RabbitMQ
        var rabbitMq = configuration.GetSection("RabbitMq");
        var rabbitHost = rabbitMq["Host"] ?? "localhost";
        var rabbitPort = ushort.TryParse(rabbitMq["Port"], out var port) ? port : (ushort)5673;
        var rabbitUser = rabbitMq["Username"] ?? "root";
        var rabbitPass = rabbitMq["Password"] ?? "rootpassword";

        services.AddMassTransit(x =>
        {
            x.AddConsumer<Consumers.ParseBatchConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitHost, rabbitPort, "/", h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                cfg.ReceiveEndpoint("parse-batch-job", e =>
                {
                    e.ConfigureConsumer<Consumers.ParseBatchConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(15),
                        TimeSpan.FromSeconds(30)));
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });

        // Zip limits + ingestion (grouping strategy)
        services.Configure<Consumers.ZipLimits>(configuration.GetSection(Consumers.ZipLimits.SectionName));
        services.Configure<Consumers.IngestionOptions>(configuration.GetSection(Consumers.IngestionOptions.SectionName));

        // Redis
        var redisConnString = configuration.GetSection("Redis")["ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConnString))
        {
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp => 
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnString));
        }

        // GradingService Client (fail-fast: required for lecturer authorization)
        var gradingServiceUrl = configuration.GetValue<string>("GradingServiceUrl");
        if (string.IsNullOrWhiteSpace(gradingServiceUrl))
        {
            throw new InvalidOperationException(
                "GradingServiceUrl is missing. It is required to enforce lecturer alias-range authorization.");
        }

        var internalApiKey = configuration.GetSection("InternalAuth")["ApiKey"];
        if (string.IsNullOrWhiteSpace(internalApiKey))
        {
            throw new InvalidOperationException(
                "InternalAuth:ApiKey is missing. It is required for GradingService M2M calls.");
        }

        services.AddHttpClient<Application.Interfaces.IGradingServiceClient, Services.GradingServiceClient>(client =>
        {
            client.BaseAddress = new Uri(gradingServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
        });

        // JWT Authentication
        var jwtSettings = configuration.GetSection("JwtSettings");

        var secret = jwtSettings["Secret"];
        if (!string.IsNullOrEmpty(secret))
        {
            var key = System.Text.Encoding.ASCII.GetBytes(secret);
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = System.TimeSpan.Zero
                };
            });
        }

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

        // Create indexes for student_papers and paper_files
        await CreateIndexesAsync(database);
    }

    private static async Task CreateIndexesAsync(IMongoDatabase database)
    {
        var papersCollection = database.GetCollection<Persistence.Documents.StudentPaper>(
            MongoCollectionNames.For<Persistence.Documents.StudentPaper>());

        await papersCollection.Indexes.CreateManyAsync([
            new CreateIndexModel<Persistence.Documents.StudentPaper>(
                Builders<Persistence.Documents.StudentPaper>.IndexKeys.Ascending(p => p.BatchId)),
            new CreateIndexModel<Persistence.Documents.StudentPaper>(
                Builders<Persistence.Documents.StudentPaper>.IndexKeys.Ascending(p => p.SubjectId)),
            new CreateIndexModel<Persistence.Documents.StudentPaper>(
                Builders<Persistence.Documents.StudentPaper>.IndexKeys.Ascending(p => p.Status)),
            new CreateIndexModel<Persistence.Documents.StudentPaper>(
                Builders<Persistence.Documents.StudentPaper>.IndexKeys.Ascending(p => p.UploadedBy)),
            new CreateIndexModel<Persistence.Documents.StudentPaper>(
                Builders<Persistence.Documents.StudentPaper>.IndexKeys
                    .Ascending(p => p.BatchId)
                    .Ascending(p => p.AliasNumber),
                new CreateIndexOptions { Unique = true })
        ]);

        var filesCollection = database.GetCollection<Domain.Entities.PaperFile>(
            MongoCollectionNames.For<Domain.Entities.PaperFile>());

        await filesCollection.Indexes.CreateManyAsync([
            new CreateIndexModel<Domain.Entities.PaperFile>(
                Builders<Domain.Entities.PaperFile>.IndexKeys.Ascending(f => f.StudentPaperId)),
            new CreateIndexModel<Domain.Entities.PaperFile>(
                Builders<Domain.Entities.PaperFile>.IndexKeys
                    .Ascending(f => f.StudentPaperId)
                    .Ascending(f => f.S3Key),
                new CreateIndexOptions { Unique = true })
        ]);
    }
}
