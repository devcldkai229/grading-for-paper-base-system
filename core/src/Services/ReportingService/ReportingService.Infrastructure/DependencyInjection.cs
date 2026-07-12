using BuildingBlocks.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReportingService.Domain.Enums;

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

        services.AddScoped<Application.Interfaces.IFeedbackReportService, Services.FeedbackReportService>();
        RegisterGradingServiceClient(services, configuration);
        RegisterJwtAuthentication(services, configuration);
        RegisterAuthorization(services);

        return services;
    }

    private static void RegisterGradingServiceClient(IServiceCollection services, IConfiguration configuration)
    {
        var internalApiKey = Environment.GetEnvironmentVariable("INTERNAL_API_KEY")
            ?? configuration.GetSection("InternalAuth")["ApiKey"]
            ?? throw new InvalidOperationException(
                "Internal API key missing. Set INTERNAL_API_KEY or InternalAuth:ApiKey.");

        var gradingServiceUrl = configuration.GetValue<string>("GradingServiceUrl")
            ?? throw new InvalidOperationException(
                "GradingServiceUrl is missing. It is required to fetch releasable feedback for export.");

        services.AddHttpClient<Application.Interfaces.IGradingServiceClient, Clients.GradingServiceClient>(client =>
        {
            client.BaseAddress = new Uri(gradingServiceUrl);
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
