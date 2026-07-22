using BuildingBlocks.EfCore;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = Persistence.NpgsqlNotificationConfiguration.CreateDataSource(connectionString);
        services.AddSingleton(dataSource);
        services.AddDbContext<Persistence.NotificationDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MapEnum<NotificationChannel>("notification_channel");
                npgsql.MapEnum<NotificationStatus>("notification_status");
                npgsql.MapEnum<NotificationType>("notification_type");
            }));

        services.AddSingleton(new NotificationDatabaseSettings(connectionString));

        // Repositories + application services
        services.AddScoped<INotificationRepository, Repositories.NotificationRepository>();
        services.AddScoped<INotificationDispatchService, Application.Services.NotificationDispatchService>();
        services.AddScoped<INotificationQueryService, Application.Services.NotificationQueryService>();

        RegisterJwtAuthentication(services, configuration);

        // Redis (idempotency dedup for consumers)
        var redisConnString = configuration.GetSection("Redis")["ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConnString))
        {
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnString));
        }
        services.AddSingleton<Consumers.NotificationIdempotencyGuard>();

        // MassTransit + RabbitMQ (consume-only — this service publishes nothing)
        var rabbitMq = configuration.GetSection("RabbitMq");
        var rabbitHost = rabbitMq["Host"] ?? "localhost";
        var rabbitPort = ushort.TryParse(rabbitMq["Port"], out var port) ? port : (ushort)5673;
        var rabbitUser = rabbitMq["Username"] ?? "root";
        var rabbitPass = rabbitMq["Password"] ?? "rootpassword";

        services.AddMassTransit(x =>
        {
            x.AddConsumer<Consumers.AssignmentNotificationConsumer>();
            x.AddConsumer<Consumers.DeadlineReminderConsumer>();
            x.AddConsumer<Consumers.ExportReadyConsumer>();
            x.AddConsumer<Consumers.RegradeNotificationConsumer>();

            x.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(rabbitHost, rabbitPort, "/", h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPass);
                });

                cfg.ReceiveEndpoint("notification-assignment", e =>
                {
                    e.ConfigureConsumer<Consumers.AssignmentNotificationConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)));
                });

                cfg.ReceiveEndpoint("notification-deadline-reminder", e =>
                {
                    e.ConfigureConsumer<Consumers.DeadlineReminderConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)));
                });

                cfg.ReceiveEndpoint("notification-export-ready", e =>
                {
                    e.ConfigureConsumer<Consumers.ExportReadyConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)));
                });

                cfg.ReceiveEndpoint("notification-regrade", e =>
                {
                    e.ConfigureConsumer<Consumers.RegradeNotificationConsumer>(ctx);
                    e.UseMessageRetry(r => r.Intervals(
                        TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)));
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
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
            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notifications"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();
    }

    public static async Task MigrateNotificationDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.NotificationDbContext>();
        var databaseSettings = scope.ServiceProvider.GetRequiredService<NotificationDatabaseSettings>();
        var logger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .CreateLogger("Notification.DatabaseMigration");

        await PostgresDatabaseMigrator.MigrateAsync(
            context, databaseSettings.ConnectionString, logger);
    }
}
