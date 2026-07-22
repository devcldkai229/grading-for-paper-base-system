using BuildingBlocks.AwsS3;
using BuildingBlocks.EfCore;
using ExamCatalogService.Infrastructure.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExamCatalogService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddExamCatalogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = Persistence.NpgsqlExamCatalogConfiguration.CreateDataSource(connectionString);
        services.AddSingleton(dataSource);
        services.AddDbContext<Persistence.ExamCatalogDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MapEnum<Domain.Enums.ExamType>("exam_type");
                npgsql.MapEnum<Domain.Enums.SubjectStatus>("subject_status");
            }));

        services.AddSingleton(new ExamCatalogDatabaseSettings(connectionString));

        services.Configure<Auth.InternalAuthSettings>(
            configuration.GetSection(Auth.InternalAuthSettings.SectionName));

        // Repositories & domain services
        services.AddScoped<Application.Interfaces.IExamCatalogRepository, Repositories.ExamCatalogRepository>();
        services.AddScoped<Application.Interfaces.IScoreGridTemplateRepository, Repositories.ScoreGridTemplateRepository>();
        services.AddScoped<Application.Interfaces.ISubjectAdminService, Services.SubjectAdminService>();
        services.AddScoped<Services.SubjectFilePreviewService>();
        services.AddScoped<Application.Interfaces.ISubjectQueryService, Application.Services.SubjectQueryService>();
        services.AddScoped<Application.Interfaces.IGradingContractService, Services.GradingContractService>();

        // S3 (AWS) — required for pre-signed URLs and uploads
        services.AddAwsS3Client(configuration);
        services.AddScoped<Application.Interfaces.IS3Service, Services.S3Service>();

        RegisterAiGradingClient(services, configuration);
        RegisterGotenbergClient(services, configuration);
        RegisterMessaging(services, configuration);
        RegisterJwtAuthentication(services, configuration);
        RegisterAuthorization(services);

        return services;
    }

    private static void RegisterMessaging(IServiceCollection services, IConfiguration configuration)
    {
        var rabbitMq = configuration.GetSection("RabbitMq");
        var rabbitHost = rabbitMq["Host"] ?? "localhost";
        var rabbitPort = ushort.TryParse(rabbitMq["Port"], out var port) ? port : (ushort)5673;
        var rabbitUser = rabbitMq["Username"] ?? "root";
        var rabbitPass = rabbitMq["Password"] ?? "rootpassword";

        services.AddMassTransit(x =>
        {
            // Replicates GradingService allocations into the local marker_assignment_view projection.
            x.AddConsumer<MarkerAssignmentChangedConsumer>();
            // Runs the long-running barem ingestion off the HTTP request thread.
            x.AddConsumer<RubricVersionChangedConsumer>();

            // Transactional outbox (publish) + inbox (idempotent consume) — N7 + N8.
            // Critical here: uploading a rubric commits the new version to Postgres and then publishes
            // RubricVersionChanged to trigger ingestion. Without the outbox those are two independent
            // writes — a broker outage or a crash in between loses the event silently, leaving the
            // subject with a fresh barem and no compiled contract while the admin sees "upload
            // successful". The outbox makes the version bump and the trigger commit atomically.
            x.AddEntityFrameworkOutbox<Persistence.ExamCatalogDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitHost, rabbitPort, "/", h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                cfg.ReceiveEndpoint("marker-assignment-changed-examcatalog", e =>
                {
                    e.UseEntityFrameworkOutbox<Persistence.ExamCatalogDbContext>(ctx);
                    e.ConfigureConsumer<MarkerAssignmentChangedConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(15),
                        TimeSpan.FromSeconds(30)));
                });

                cfg.ReceiveEndpoint("rubric-version-changed-examcatalog", e =>
                {
                    e.UseEntityFrameworkOutbox<Persistence.ExamCatalogDbContext>(ctx);
                    e.ConfigureConsumer<RubricVersionChangedConsumer>(ctx);
                    // Ingestion is expensive and can time out on transient LLM issues — retry sparsely.
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(30),
                        TimeSpan.FromMinutes(2)));
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });
    }

    private static void RegisterAiGradingClient(IServiceCollection services, IConfiguration configuration)
    {
        var internalApiKey = Environment.GetEnvironmentVariable("INTERNAL_API_KEY")
            ?? configuration.GetValue<string>("AiGrading:InternalApiKey")
            ?? configuration.GetSection(Auth.InternalAuthSettings.SectionName)["ApiKey"]
            ?? throw new InvalidOperationException(
                "AI internal API key missing. Set INTERNAL_API_KEY or InternalAuth:ApiKey.");

        var aiGradingUrl = configuration.GetValue<string>("AiGradingServiceUrl")
            ?? "http://localhost:8080";

        services.AddHttpClient<Application.Interfaces.IAiGradingClient, Clients.AiGradingClient>(client =>
        {
            client.BaseAddress = new Uri(aiGradingUrl);
            // Rubric ingestion (render + 2-pass extract + per-leaf compile) can take several minutes.
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
        });
    }

    private static void RegisterAuthorization(IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                "AdminOnly",
                policy => policy.RequireAssertion(ctx =>
                    ctx.User.HasClaim(c => c.Type == "Role" && c.Value == "Admin")));
        });
    }

    private static void RegisterGotenbergClient(IServiceCollection services, IConfiguration configuration)
    {
        var gotenbergUrl = configuration.GetValue<string>("GotenbergUrl")
            ?? "http://localhost:3000";

        services.AddHttpClient<Application.Interfaces.IGotenbergClient, Clients.GotenbergClient>(client =>
        {
            client.BaseAddress = new Uri(gotenbergUrl);
            client.Timeout = TimeSpan.FromMinutes(3);
        });
    }

    private static void RegisterJwtAuthentication(IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secret = jwtSettings["Secret"];
        if (string.IsNullOrEmpty(secret)) return;

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

    public static async Task MigrateExamCatalogDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.ExamCatalogDbContext>();
        var databaseSettings = scope.ServiceProvider.GetRequiredService<ExamCatalogDatabaseSettings>();
        var migrateLogger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .CreateLogger("ExamCatalog.DatabaseMigration");
        var seedLogger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILogger<Seed.ExamCatalogDbContextSeed>>();

        await PostgresDatabaseMigrator.MigrateAsync(
            context, databaseSettings.ConnectionString, migrateLogger);
        await Seed.ExamCatalogDbContextSeed.SeedAsync(context, seedLogger);
    }
}
