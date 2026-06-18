using Microsoft.OpenApi.Models;
using SubmissionService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Submission Service API", Version = "v1" });
});
builder.Services.AddSubmissionInfrastructure(builder.Configuration);

var app = builder.Build();

await app.Services.VerifySubmissionDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Submission Service API v1"));
}

// Note: No UseHttpsRedirection() — this service sits behind the API Gateway which handles HTTPS

app.UseAuthorization();

app.MapControllers();

app.Run();
