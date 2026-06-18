using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Infrastructure.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options)
    {
    }

    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<NotificationChannel>(name: "notification_channel");
        modelBuilder.HasPostgresEnum<NotificationStatus>(name: "notification_status");
        modelBuilder.HasPostgresEnum<NotificationType>(name: "notification_type");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
