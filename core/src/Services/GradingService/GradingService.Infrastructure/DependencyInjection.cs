using BuildingBlocks.AspNetCore.Observability;
using BuildingBlocks.EfCore;
using Gradepaper.ExamCatalog.V1;
using Gradepaper.Submission.V1;
using GradingService.Application.Interfaces;
using GradingService.Domain.Enums;
using GradingService.Infrastructure.Auth;
using GradingService.Infrastructure.Clients.Grpc;
using GradingService.Infrastructure.Consumers;
using GradingService.Infrastructure.Files;
using GradingService.Infrastructure.Persistence.Repositories;
using Grpc.Core;
using Grpc.Net.Client.Configuration;
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

        // outbox_pending_messages gauge (§9.1b) — surfaces outbox backlog for this service.
        services.AddOutboxPendingMetric<Persistence.GradingDbContext>();

        services.Configure<InternalAuthSettings>(configuration.GetSection(InternalAuthSettings.SectionName));

        services.AddScoped<IGradingAssignmentRepository, GradingAssignmentRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IGradingResumePointerRepository, GradingResumePointerRepository>();
        services.AddScoped<IMarkerAssignmentRepository, MarkerAssignmentRepository>();
        services.AddScoped<ILecturerMarkerCodeRepository, LecturerMarkerCodeRepository>();
        services.AddScoped<IUnitOfWork, GradingUnitOfWork>();
        services.AddScoped<IGradeExportFileBuilder, MiniExcelGradeExportFileBuilder>();
        services.AddScoped<IGradingSessionService, Application.Services.GradingSessionService>();
        services.AddScoped<IMessagePublisher, Messaging.MassTransitMessagePublisher>();

        services.Configure<Jobs.DeadlineReminderOptions>(configuration.GetSection(Jobs.DeadlineReminderOptions.SectionName));
        services.AddHostedService<Jobs.DeadlineReminderBackgroundService>();

        RegisterGrpcClients(services, configuration);
        RegisterJwtAuthentication(services, configuration);
        RegisterMessaging(services, configuration);

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
            // Results from the Python AI worker (AiGradeCompleted/Failed) — single writer for AiStatus.
            x.AddConsumer<AiGradeCompletedConsumer>();
            x.AddConsumer<AiGradeFailedConsumer>();

            // Replicates IamService lecturer profiles into the local marker-code projection.
            x.AddConsumer<LecturerProfileChangedConsumer>();

            // Transactional outbox (publish) + inbox (idempotent consume) — N7 + N8.
            x.AddEntityFrameworkOutbox<Persistence.GradingDbContext>(o =>
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

                cfg.ReceiveEndpoint("ai-grade-completed", e =>
                {
                    e.UseEntityFrameworkOutbox<Persistence.GradingDbContext>(ctx);
                    e.ConfigureConsumer<AiGradeCompletedConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(30),
                        TimeSpan.FromSeconds(60)));
                });

                cfg.ReceiveEndpoint("ai-grade-failed", e =>
                {
                    e.UseEntityFrameworkOutbox<Persistence.GradingDbContext>(ctx);
                    e.ConfigureConsumer<AiGradeFailedConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(30)));
                });

                cfg.ReceiveEndpoint("lecturer-profile-changed-grading", e =>
                {
                    e.UseEntityFrameworkOutbox<Persistence.GradingDbContext>(ctx);
                    e.ConfigureConsumer<LecturerProfileChangedConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(15),
                        TimeSpan.FromSeconds(30)));
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });
    }

    /// <summary>
    /// Registers the ExamCatalog/Submission east-west clients over gRPC (Phase 2), sitting behind the
    /// unchanged Application interfaces. IamService marker codes are replicated locally via
    /// LecturerProfileChanged (N5) — no synchronous IAM client is needed.
    /// </summary>
    private static void RegisterGrpcClients(IServiceCollection services, IConfiguration configuration)
    {
        var examCatalogGrpcUrl = configuration.GetValue<string>("ExamCatalogGrpcUrl")
            ?? throw new InvalidOperationException("ExamCatalogGrpcUrl is missing.");
        var submissionGrpcUrl = configuration.GetValue<string>("SubmissionGrpcUrl")
            ?? throw new InvalidOperationException("SubmissionGrpcUrl is missing.");

        services.AddSingleton<GrpcClientAuthInterceptor>();

        // gRPC-native retry for idempotent reads (transient Unavailable only). Applied per-channel.
        var retryServiceConfig = new ServiceConfig
        {
            MethodConfigs =
            {
                new MethodConfig
                {
                    Names = { MethodName.Default },
                    RetryPolicy = new RetryPolicy
                    {
                        MaxAttempts = 3,
                        InitialBackoff = TimeSpan.FromMilliseconds(200),
                        MaxBackoff = TimeSpan.FromSeconds(2),
                        BackoffMultiplier = 2,
                        RetryableStatusCodes = { StatusCode.Unavailable }
                    }
                }
            }
        };

        // The global standard resilience handler (Phase 0) breaks gRPC streaming/deadlines — gRPC has
        // its own retry/deadline, so strip the HTTP resilience pipeline off these clients.
        // RemoveAllResilienceHandlers is [Experimental] (EXTEXP0001); suppress that specific warning.
#pragma warning disable EXTEXP0001
        services.AddGrpcClient<RubricService.RubricServiceClient>(o => o.Address = new Uri(examCatalogGrpcUrl))
            .AddInterceptor<GrpcClientAuthInterceptor>()
            .ConfigureChannel(ch => ch.ServiceConfig = retryServiceConfig)
            .RemoveAllResilienceHandlers();

        services.AddGrpcClient<PaperService.PaperServiceClient>(o => o.Address = new Uri(submissionGrpcUrl))
            .AddInterceptor<GrpcClientAuthInterceptor>()
            .ConfigureChannel(ch => ch.ServiceConfig = retryServiceConfig)
            .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        services.AddScoped<IExamCatalogServiceClient, ExamCatalogGrpcClient>();
        services.AddScoped<ISubmissionServiceClient, PaperGrpcClient>();
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
