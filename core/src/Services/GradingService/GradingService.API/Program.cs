using BuildingBlocks.AspNetCore.Extensions;
using GradingService.Infrastructure;
using GradingService.Infrastructure.Auth;
using GradingService.Infrastructure.Middleware;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformObservability("grading-service");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Grading Service API", Version = "v1" });
});

builder.Services.Configure<InternalAuthSettings>(builder.Configuration.GetSection(InternalAuthSettings.SectionName));

var internalApiKey = builder.Configuration.GetSection(InternalAuthSettings.SectionName)["ApiKey"];
if (string.IsNullOrWhiteSpace(internalApiKey))
{
    throw new InvalidOperationException($"{InternalAuthSettings.SectionName}:ApiKey is required for internal endpoints.");
}

builder.Services.AddGradingInfrastructure(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);
}

var app = builder.Build();

await app.Services.MigrateGradingDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Grading Service API v1"));
}

app.UseGlobalExceptionHandling();
app.UseCorrelationId();
app.UseMiddleware<InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapPlatformHealthChecks();

app.MapControllers();

app.Run();
