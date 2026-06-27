using BuildingBlocks.EfCore;
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
                ClockSkew = System.TimeSpan.Zero
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