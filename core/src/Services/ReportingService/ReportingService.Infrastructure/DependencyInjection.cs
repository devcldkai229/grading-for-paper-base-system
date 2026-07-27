using BuildingBlocks.AspNetCore.Observability;
using BuildingBlocks.EfCore;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReportingService.Domain.Enums;
using ReportingService.Infrastructure.Consumers;

namespace ReportingService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddReportingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = Persistence.NpgsqlReportingConfiguration.CreateDataSource(connectionString);
        services.AddSingleton(dataSource);
        services.AddDbContext<Persistence.ReportingDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MapEnum<ExportStatus>("export_status");
            }));

        services.AddSingleton(new ReportingDatabaseSettings(connectionString));

        // outbox_pending_messages gauge (§9.1b). Reporting is consumer-only but still runs the EF
        // outbox table (inbox/outbox share the schema), so the backlog signal is meaningful here too.
        services.AddOutboxPendingMetric<Persistence.ReportingDbContext>();

        services.AddScoped<Application.Interfaces.IExportJobRepository, Persistence.Repositories.ExportJobRepository>();
        services.AddScoped<Application.Interfaces.IReportFileService, Files.MiniExcelReportFileService>();

        // Local read models populated by the event consumers (replace the former REST aggregation).
        services.AddScoped<Application.Interfaces.IProgressReadRepository, Persistence.Repositories.ProgressReadRepository>();
        services.AddScoped<Application.Interfaces.IScoreRecordReadRepository, Persistence.Repositories.ScoreRecordReadRepository>();
        services.AddScoped<Application.Interfaces.IFeedbackRecordReadRepository, Persistence.Repositories.FeedbackRecordReadRepository>();
        services.AddScoped<Application.Interfaces.IAuditRecordReadRepository, Persistence.Repositories.AuditRecordReadRepository>();

        services.AddScoped<Application.Interfaces.IFeedbackReportService, Application.Services.FeedbackReportService>();
        services.AddScoped<Application.Interfaces.IGradingProgressService, Application.Services.GradingProgressService>();
        services.AddScoped<Application.Interfaces.IScoreDistributionService, Application.Services.ScoreDistributionService>();
        services.AddScoped<Application.Interfaces.IPassFailReportService, Application.Services.PassFailReportService>();
        services.AddScoped<Application.Interfaces.IGlobalAuditLogService, Application.Services.GlobalAuditLogService>();
        // Grading + Iam data is now event-sourced into local projections; only ExamCatalog (/search) and
        // Submission (/audit-logs) remain synchronous REST dependencies.
        RegisterExamCatalogServiceClient(services, configuration);
        RegisterSubmissionServiceClient(services, configuration);
        RegisterJwtAuthentication(services, configuration);
        RegisterAuthorization(services);
        RegisterMessaging(services, configuration);

        return services;
    }

    private static void RegisterMessaging(IServiceCollection services, IConfiguration configuration)
    {
        var rabbitMq = configuration.GetSection("RabbitMq");
        var rabbitHost = rabbitMq["Host"] ?? "localhost";
        var rabbitPort = ushort.TryParse(rabbitMq["Port"], out var p) ? p : (ushort)5672;
        var rabbitUser = rabbitMq["Username"] ?? "root";
        var rabbitPass = rabbitMq["Password"] ?? "rootpassword";

        services.AddMassTransit(x =>
        {
            // Consumer-only: Reporting builds local read models from Grading + Iam events.
            x.AddConsumer<GradingAssignmentStatusChangedConsumer>();
            x.AddConsumer<ScoreSubmittedConsumer>();
            x.AddConsumer<ScoreOverriddenConsumer>();
            x.AddConsumer<AuditLogRecordedConsumer>();

            // EF inbox for idempotent consume (N8). No UseBusOutbox — Reporting never publishes.
            x.AddEntityFrameworkOutbox<Persistence.ReportingDbContext>(o => o.UsePostgres());

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitHost, rabbitPort, "/", h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                cfg.ReceiveEndpoint("grading-assignment-status-changed-reporting", e =>
                {
                    e.UseEntityFrameworkOutbox<Persistence.ReportingDbContext>(ctx);
                    e.ConfigureConsumer<GradingAssignmentStatusChangedConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)));
                });

                cfg.ReceiveEndpoint("score-submitted-reporting", e =>
                {
                    e.UseEntityFrameworkOutbox<Persistence.ReportingDbContext>(ctx);
                    e.ConfigureConsumer<ScoreSubmittedConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)));
                });

                cfg.ReceiveEndpoint("score-overridden-reporting", e =>
                {
                    e.UseEntityFrameworkOutbox<Persistence.ReportingDbContext>(ctx);
                    e.ConfigureConsumer<ScoreOverriddenConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)));
                });

                cfg.ReceiveEndpoint("audit-log-recorded-reporting", e =>
                {
                    e.UseEntityFrameworkOutbox<Persistence.ReportingDbContext>(ctx);
                    e.ConfigureConsumer<AuditLogRecordedConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)));
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });
    }

    private static string GetInternalApiKey(IConfiguration configuration) =>
        Environment.GetEnvironmentVariable("INTERNAL_API_KEY")
            ?? configuration.GetSection("InternalAuth")["ApiKey"]
            ?? throw new InvalidOperationException(
                "Internal API key missing. Set INTERNAL_API_KEY or InternalAuth:ApiKey.");

    private static void RegisterExamCatalogServiceClient(IServiceCollection services, IConfiguration configuration)
    {
        var internalApiKey = GetInternalApiKey(configuration);

        var examCatalogServiceUrl = configuration.GetValue<string>("ExamCatalogServiceUrl")
            ?? throw new InvalidOperationException(
                "ExamCatalogServiceUrl is missing. It is required for the grading progress dashboard's semester/exam filter.");

        services.AddHttpClient<Application.Interfaces.IExamCatalogServiceClient, Clients.ExamCatalogServiceClient>(client =>
        {
            client.BaseAddress = new Uri(examCatalogServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
        });
    }

    private static void RegisterSubmissionServiceClient(IServiceCollection services, IConfiguration configuration)
    {
        var internalApiKey = GetInternalApiKey(configuration);

        var submissionServiceUrl = configuration.GetValue<string>("SubmissionServiceUrl")
            ?? throw new InvalidOperationException(
                "SubmissionServiceUrl is missing. It is required for the global audit log viewer.");

        services.AddHttpClient<Application.Interfaces.ISubmissionServiceClient, Clients.SubmissionServiceClient>(client =>
        {
            client.BaseAddress = new Uri(submissionServiceUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
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
                ClockSkew = System.TimeSpan.Zero
            };
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

    public static async Task MigrateReportingDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.ReportingDbContext>();
        var databaseSettings = scope.ServiceProvider.GetRequiredService<ReportingDatabaseSettings>();
        var logger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .CreateLogger("Reporting.DatabaseMigration");

        await PostgresDatabaseMigrator.MigrateAsync(
            context, databaseSettings.ConnectionString, logger);
    }
}
