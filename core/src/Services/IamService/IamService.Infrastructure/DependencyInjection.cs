using BuildingBlocks.AspNetCore.Observability;
using BuildingBlocks.EfCore;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data;

namespace IamService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIamInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

        var dataSource = Persistence.NpgsqlIamConfiguration.CreateDataSource(connectionString);
        services.AddSingleton(dataSource);
        services.AddDbContext<Persistence.IamDbContext>(options =>
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.MapEnum<Domain.Enums.LoginProvider>("login_provider");
                npgsql.MapEnum<Domain.Enums.UserStatus>("user_status");
                npgsql.MapEnum<Domain.Enums.UserRole>("user_role");
            }));

        services.AddSingleton(new IamDatabaseSettings(connectionString));

        // outbox_pending_messages gauge (§9.1b) — surfaces outbox backlog for this service.
        services.AddOutboxPendingMetric<Persistence.IamDbContext>();

        // Repositories
        services.AddScoped<IamService.Application.Interfaces.IUserRepository, Persistence.Repositories.UserRepository>();
        services.AddScoped<IamService.Application.Interfaces.IRefreshTokenRepository, Persistence.Repositories.RefreshTokenRepository>();

        // Settings
        services.Configure<Services.JwtSettings>(configuration.GetSection("JwtSettings"));
        services.Configure<Services.GoogleAuthSettings>(configuration.GetSection("GoogleAuth"));

        // Services
        services.AddScoped<IamService.Application.Interfaces.ITokenService, Services.TokenService>();
        services.AddScoped<IamService.Application.Interfaces.IGoogleAuthService, Services.GoogleAuthService>();
        services.AddScoped<IamService.Application.Interfaces.IAuthService, IamService.Application.Services.AuthService>();
        services.AddScoped<IamService.Application.Interfaces.IUserService, IamService.Application.Services.UserService>();
        services.AddScoped<IamService.Application.Interfaces.IMessagePublisher, Messaging.MassTransitMessagePublisher>();

        RegisterMessaging(services, configuration);

        // JWT Authentication middleware
        var jwtSettings = configuration.GetSection("JwtSettings").Get<Services.JwtSettings>();
        if (jwtSettings != null)
        {
            var key = System.Text.Encoding.ASCII.GetBytes(jwtSettings.Secret);
            var tokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = System.TimeSpan.FromMinutes(1)
            };

            services.AddSingleton(tokenValidationParameters);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = tokenValidationParameters;
            });
        }

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireAssertion(ctx =>
                    ctx.User.HasClaim(c => c.Type == "Role" && c.Value == "Admin")));
        });

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
            // Publishes LecturerProfileChanged atomically with user changes (N7). Producer only.
            x.AddEntityFrameworkOutbox<Persistence.IamDbContext>(o =>
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

                cfg.ConfigureEndpoints(ctx);
            });
        });
    }

    public static async Task MigrateIamDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.IamDbContext>();
        var databaseSettings = scope.ServiceProvider.GetRequiredService<IamDatabaseSettings>();
        var migrateLogger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .CreateLogger("Iam.DatabaseMigration");
        var seedLogger = scope.ServiceProvider
            .GetRequiredService<Microsoft.Extensions.Logging.ILogger<IamService.Infrastructure.Seed.IamDbContextSeed>>();

        await PostgresDatabaseMigrator.MigrateAsync(
            context, databaseSettings.ConnectionString, migrateLogger);
        await IamService.Infrastructure.Seed.IamDbContextSeed.SeedAsync(context, seedLogger);
    }
}