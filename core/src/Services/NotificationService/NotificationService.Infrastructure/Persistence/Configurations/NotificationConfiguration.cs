using NotificationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NotificationService.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Ignore(x => x.UpdatedAt);

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasColumnType("notification_type")
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasColumnName("channel")
            .HasColumnType("notification_channel")
            .IsRequired();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.Body)
            .HasColumnName("body");

        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("notification_status")
            .IsRequired();

        builder.Property(x => x.IsRead)
            .HasColumnName("is_read")
            .IsRequired();

        builder.Property(x => x.SentAt)
            .HasColumnName("sent_at");

        builder.Property(x => x.ReadAt)
            .HasColumnName("read_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("idx_notifications_user");

        builder.HasIndex(x => new { x.UserId, x.IsRead })
            .HasDatabaseName("idx_notifications_unread")
            .HasFilter("is_read = FALSE");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("idx_notifications_status");
    }
}
