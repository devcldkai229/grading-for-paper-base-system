using BuildingBlocks.AwsS3;
using BuildingBlocks.EfCore;
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
        services.AddScoped<Application.Interfaces.ISubjectAdminService, Services.SubjectAdminService>();
        services.AddScoped<Services.SubjectFilePreviewService>();

        // S3 (AWS) — required for pre-signed URLs and uploads
        services.AddAwsS3Client(configuration);
        services.AddScoped<Application.Interfaces.IS3Service, Services.S3Service>();

        RegisterAiGradingClient(services, configuration);
        RegisterGotenbergClient(services, configuration);
        RegisterJwtAuthentication(services, configuration);
        RegisterAuthorization(services);

        return services;
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
            client.Timeout = TimeSpan.FromMinutes(2);
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
