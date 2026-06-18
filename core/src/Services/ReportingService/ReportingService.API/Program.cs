using Microsoft.OpenApi.Models;
using ReportingService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Reporting Service API", Version = "v1" });
});
builder.Services.AddReportingInfrastructure(builder.Configuration);

var app = builder.Build();

await app.Services.MigrateReportingDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Reporting Service API v1"));
}

app.UseAuthorization();

app.MapControllers();

app.Run();
