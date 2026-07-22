using BuildingBlocks.AspNetCore.Extensions;
using IamService.Infrastructure;
using IamService.Infrastructure.Auth;
using IamService.Infrastructure.Middleware;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformObservability("iam-service");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "IAM Service API", Version = "v1" });

    // Configure JWT authentication in Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 12345abcdef\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,
            },
            new string[] {}
        }
    });
});
builder.Services.AddIamInfrastructure(builder.Configuration);

builder.Services.Configure<InternalAuthSettings>(builder.Configuration.GetSection(InternalAuthSettings.SectionName));

var internalApiKey = builder.Configuration.GetSection(InternalAuthSettings.SectionName)["ApiKey"];
if (string.IsNullOrWhiteSpace(internalApiKey))
{
    throw new InvalidOperationException($"{InternalAuthSettings.SectionName}:ApiKey is required for internal endpoints.");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);
}

var app = builder.Build();

await app.Services.MigrateIamDatabaseAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "IAM Service API v1"));
}

// Note: No UseHttpsRedirection() — this service sits behind the API Gateway which handles HTTPS

app.UseGlobalExceptionHandling();
app.UseCorrelationId();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapPlatformHealthChecks();

app.MapControllers();


app.Run();
