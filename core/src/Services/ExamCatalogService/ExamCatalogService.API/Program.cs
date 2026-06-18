using ExamCatalogService.Infrastructure;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Exam Catalog Service API", Version = "v1" });
});
builder.Services.AddExamCatalogInfrastructure(builder.Configuration);

var app = builder.Build();

await app.Services.MigrateExamCatalogDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Exam Catalog Service API v1"));
}

// Note: No UseHttpsRedirection() — this service sits behind the API Gateway which handles HTTPS

app.MapControllers();

app.Run();
