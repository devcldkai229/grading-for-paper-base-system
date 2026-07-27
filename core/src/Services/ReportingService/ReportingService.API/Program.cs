using BuildingBlocks.AspNetCore.Extensions;
using Microsoft.OpenApi.Models;
using ReportingService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformObservability("reporting-service");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Reporting Service API", Version = "v1" });
});
builder.Services.AddReportingInfrastructure(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);
}

var app = builder.Build();

await app.Services.MigrateReportingDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Reporting Service API v1"));
}

app.UseGlobalExceptionHandling();
app.UseCorrelationId();
app.UseAuthentication();
app.UseAuthorization();

app.MapPlatformHealthChecks();

app.MapControllers();

app.LogPlatformStartupBanner("reporting-service");
app.Run();
