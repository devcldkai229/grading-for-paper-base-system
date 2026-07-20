using BuildingBlocks.EfCore;
using GradingService.Application.Interfaces;
using GradingService.Domain.Enums;
using GradingService.Infrastructure.Auth;
using GradingService.Infrastructure.Clients;
using GradingService.Infrastructure.Files;
using GradingService.Infrastructure.Persistence.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GradingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGradingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = Persistence.NpgsqlGradingConfiguration.CreateDataSource(connectionString);
        services.AddSingleton(dataSource);
        services.AddDbContext<Persistence.GradingDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MapEnum<AssignmentType>("assignment_type");
                npgsql.MapEnum<GradingProgressStatus>("grading_progress_status");
                npgsql.MapEnum<AiSyncStatus>("ai_sync_status");
                npgsql.MapEnum<AiReviewStatus>("ai_review_status");
            }));

        services.AddSingleton(new GradingDatabaseSettings(connectionString));

        services.Configure<InternalAuthSettings>(configuration.GetSection(InternalAuthSettings.SectionName));

        services.AddScoped<IGradingAssignmentRepository, GradingAssignmentRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IGradingResumePointerRepository, GradingResumePointerRepository>();
        services.AddScoped<IMarkerAssignmentRepository, MarkerAssignmentRepository>();
        services.AddScoped<IUnitOfWork, GradingUnitOfWork>();
        services.AddScoped<IGradeExportFileBuilder, MiniExcelGradeExportFileBuilder>();
        services.AddScoped<IGradingSessionService, Application.Services.GradingSessionService>();
        services.AddScoped<IMessagePublisher, Messaging.MassTransitMessagePublisher>();

        services.Configure<Jobs.DeadlineReminderOptions>(configuration.GetSection(Jobs.DeadlineReminderOptions.SectionName));
        services.AddHostedService<Jobs.DeadlineReminderBackgroundService>();

        RegisterInternalHttpClients(services, configuration);
        RegisterJwtAuthentication(services, configuration);
        RegisterMessaging(services, configuration);

        return services;
    }

    private static void RegisterMessaging(IServiceCollection services, IConfiguration configuration)
    {
        // Publish-only: GradingService raises business events for NotificationService to consume;
        // it has no consumers of its own.
        var rabbitMq = configuration.GetSection("RabbitMq");
        var rabbitHost = rabbitMq["Host"] ?? "localhost";
        var rabbitPort = ushort.TryParse(rabbitMq["Port"], out var port) ? port : (ushort)5673;
        var rabbitUser = rabbitMq["Username"] ?? "root";
        var rabbitPass = rabbitMq["Password"] ?? "rootpassword";

        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitHost, rabbitPort, "/", h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });
    }

    private static void RegisterInternalHttpClients(IServiceCollection services, IConfiguration configuration)
    {
        var internalApiKey = configuration.GetSection(InternalAuthSettings.SectionName)["ApiKey"]
            ?? throw new InvalidOperationException($"{InternalAuthSettings.SectionName}:ApiKey is missing.");

        var submissionUrl = configuration.GetValue<string>("SubmissionServiceUrl")
            ?? throw new InvalidOperationException("SubmissionServiceUrl is missing.");
        services.AddHttpClient<ISubmissionServiceClient, SubmissionServiceClient>(client =>
        {
            client.BaseAddress = new Uri(submissionUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
        });

        var catalogUrl = configuration.GetValue<string>("ExamCatalogServiceUrl")
            ?? throw new InvalidOperationException("ExamCatalogServiceUrl is missing.");
        services.AddHttpClient<IExamCatalogServiceClient, ExamCatalogServiceClient>(client =>
        {
            client.BaseAddress = new Uri(catalogUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
        });

        var iamUrl = configuration.GetValue<string>("IamServiceUrl")
            ?? throw new InvalidOperationException("IamServiceUrl is missing.");
        services.AddHttpClient<IIamServiceClient, Clients.IamServiceClient>(client =>
        {
            client.BaseAddress = new Uri(iamUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
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
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();
    }

    public static async Task MigrateGradingDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.GradingDbContext>();
        var databaseSettings = scope.ServiceProvider.GetRequiredService<GradingDatabaseSettings>();
        var logger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .CreateLogger("Grading.DatabaseMigration");

        await PostgresDatabaseMigrator.MigrateAsync(
            context, databaseSettings.ConnectionString, logger);
    }
}
