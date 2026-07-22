using ReportingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReportingService.Infrastructure.Persistence.Configurations;

public class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("audit_record");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(x => x.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(x => x.SourceService).HasColumnName("source_service").HasMaxLength(32).IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(128).IsRequired();
        builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(128);
        builder.Property(x => x.EntityId).HasColumnName("entity_id");
        builder.Property(x => x.OldValue).HasColumnName("old_value");
        builder.Property(x => x.NewValue).HasColumnName("new_value");
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.HasIndex(x => x.MessageId).IsUnique();
        builder.HasIndex(x => x.OccurredAt).HasDatabaseName("idx_audit_record_occurred");
    }
}
