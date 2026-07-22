using BuildingBlocks.AspNetCore.Extensions;
using Microsoft.OpenApi.Models;
using NotificationService.API.Hubs;
using NotificationService.API.Services;
using NotificationService.Application.Interfaces;
using NotificationService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddPlatformObservability("notification-service");

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddScoped<INotificationPushService, NotificationPushService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Notification Service API", Version = "v1" });
});
builder.Services.AddNotificationInfrastructure(builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "postgres", tags: ["ready"]);
}

var app = builder.Build();

await app.Services.MigrateNotificationDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service API v1"));
}

app.UseGlobalExceptionHandling();
app.UseCorrelationId();
app.UseAuthentication();
app.UseAuthorization();

app.MapPlatformHealthChecks();

app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");

app.LogPlatformStartupBanner("notification-service");
app.Run();
