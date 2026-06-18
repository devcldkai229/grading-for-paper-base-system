using NotificationService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace NotificationService.Infrastructure.Persistence.Configurations;

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("notification_templates");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Ignore(x => x.UpdatedAt);

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasColumnType("notification_type")
            .IsRequired();

        builder.Property(x => x.Channel)
            .HasColumnName("channel")
            .HasColumnType("notification_channel")
            .IsRequired();

        builder.Property(x => x.Subject)
            .HasColumnName("subject")
            .HasMaxLength(255);

        builder.Property(x => x.BodyTemplate)
            .HasColumnName("body_template")
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(x => new { x.Type, x.Channel })
            .IsUnique();
    }
}
