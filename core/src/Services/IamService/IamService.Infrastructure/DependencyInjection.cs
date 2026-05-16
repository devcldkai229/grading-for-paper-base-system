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

        services.AddDbContext<Persistence.IamDbContext>(options =>
            options.UseNpgsql(connectionString));

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

        return services;
    }

    public static async Task MigrateIamDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Persistence.IamDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IamService.Infrastructure.Seed.IamDbContextSeed>>();

        await EnsureDatabaseExistsAsync(context);
        await context.Database.MigrateAsync();

        // Seed initial data
        await IamService.Infrastructure.Seed.IamDbContextSeed.SeedAsync(context, logger);
    }

    private static async Task EnsureDatabaseExistsAsync(Persistence.IamDbContext context)
    {
        var connectionString = context.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("IAM database connection string is missing.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            throw new InvalidOperationException("IAM database name is missing.");
        }

        var targetDatabase = builder.Database;
        var maintenanceBuilder = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
        {
            Database = "postgres"
        };

        await using var connection = new NpgsqlConnection(maintenanceBuilder.ConnectionString);
        await connection.OpenAsync();

        await using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT 1 FROM pg_database WHERE datname = @databaseName";
            checkCommand.Parameters.AddWithValue("databaseName", targetDatabase);

            var existingDatabase = await checkCommand.ExecuteScalarAsync();
            if (existingDatabase is not null)
            {
                return;
            }
        }

        var quotedDatabaseName = QuoteIdentifier(targetDatabase);
        await using var createCommand = connection.CreateCommand();
        createCommand.CommandText = $"CREATE DATABASE {quotedDatabaseName}";
        await createCommand.ExecuteNonQueryAsync();
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"") }\"";
    }
}