using IamService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IamService.Infrastructure.Persistence.Configurations
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("audit_logs");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            builder.Property(x => x.UserId)
                .HasColumnName("user_id");

            builder.Property(x => x.Action)
                .HasColumnName("action")
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.EntityType)
                .HasColumnName("entity_type")
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.EntityId)
                .HasColumnName("entity_id")
                .IsRequired();

            builder.Property(x => x.OldValue)
                .HasColumnName("old_value");

            builder.Property(x => x.NewValue)
                .HasColumnName("new_value");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at");
        }
    }
}
