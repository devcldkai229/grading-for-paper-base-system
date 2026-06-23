using BuildingBlocks.AspNetCore.Extensions;
using BuildingBlocks.AspNetCore.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using SubmissionService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

const long MaxUploadBytes = 524_288_000;
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = MaxUploadBytes);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = MaxUploadBytes;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Submission Service API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});
builder.Services.AddSubmissionInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddMongoDb(
        clientFactory: sp => sp.GetRequiredService<MongoDB.Driver.IMongoClient>(),
        name: "mongodb",
        tags: ["ready"]);

var app = builder.Build();

await app.Services.VerifySubmissionDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Submission Service API v1"));
}

app.UseGlobalExceptionHandling();
app.UseCorrelationId();
app.UseMiddleware<SubmissionService.Infrastructure.Middleware.InternalApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteMinimalJson
});

app.MapControllers();

app.Run();
