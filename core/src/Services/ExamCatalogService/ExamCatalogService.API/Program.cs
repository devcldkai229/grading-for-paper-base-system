using BuildingBlocks.AspNetCore.Extensions;
using BuildingBlocks.AspNetCore.Grpc;
using ExamCatalogService.API.Services.Grpc;
using ExamCatalogService.Infrastructure;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformObservability("exam-catalog-service");

// Dedicated Http2-only (h2c) port for gRPC alongside the existing HTTP/1.1 port.
builder.AddDualProtocolGrpcHosting(defaultHttpPort: 8080, defaultGrpcPort: 5066);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Exam Catalog Service API", Version = "v1" });

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
builder.Services.AddSingleton<GrpcInternalApiKeyInterceptor>();
builder.Services.AddGrpc(o => o.Interceptors.Add<GrpcInternalApiKeyInterceptor>());
builder.Services.AddExamCatalogInfrastructure(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);
}

var app = builder.Build();

await app.Services.MigrateExamCatalogDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Exam Catalog Service API v1"));
}

// Note: No UseHttpsRedirection() — this service sits behind the API Gateway which handles HTTPS

app.UseGlobalExceptionHandling();
app.UseCorrelationId();
app.UseMiddleware<ExamCatalogService.Infrastructure.Middleware.InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapPlatformHealthChecks();

app.MapGrpcService<RubricGrpcService>();

app.MapControllers();

app.LogPlatformStartupBanner("exam-catalog-service", grpcPort: 5066);
app.Run();
