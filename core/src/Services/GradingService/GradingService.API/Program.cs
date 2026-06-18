using GradingService.Infrastructure;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Grading Service API", Version = "v1" });
});
builder.Services.AddGradingInfrastructure(builder.Configuration);

var app = builder.Build();

await app.Services.MigrateGradingDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Grading Service API v1"));
}

app.MapControllers();

app.Run();
