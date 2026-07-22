using GradingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GradingService.Infrastructure.Persistence.Configurations;

public class MarkerAssignmentConfiguration : IEntityTypeConfiguration<MarkerAssignment>
{
    public void Configure(EntityTypeBuilder<MarkerAssignment> builder)
    {
        builder.ToTable("marker_assignments", t =>
        {
            t.HasCheckConstraint("CK_marker_assignments_alias_start", "alias_start >= 1");
            t.HasCheckConstraint("CK_marker_assignments_alias_end", "alias_end >= alias_start");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Ignore(x => x.CreatedAt);
        builder.Ignore(x => x.UpdatedAt);

        builder.Property(x => x.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.Property(x => x.TeacherId)
            .HasColumnName("teacher_id")
            .IsRequired();

        builder.Property(x => x.AliasStart)
            .HasColumnName("alias_start")
            .IsRequired();

        builder.Property(x => x.AliasEnd)
            .HasColumnName("alias_end")
            .IsRequired();

        builder.Property(x => x.AssignedBy)
            .HasColumnName("assigned_by")
            .IsRequired();

        builder.Property(x => x.AssignedAt)
            .HasColumnName("assigned_at")
            .IsRequired();

        builder.Property(x => x.BatchId)
            .HasColumnName("batch_id");

        builder.Property(x => x.ZipFileName)
            .HasColumnName("zip_file_name")
            .HasMaxLength(512);

        builder.HasIndex(x => new { x.SubjectId, x.AliasStart, x.AliasEnd })
            .IsUnique();

        builder.HasIndex(x => new { x.SubjectId, x.BatchId })
            .IsUnique()
            .HasFilter("batch_id IS NOT NULL");

        builder.HasIndex(x => x.SubjectId)
            .HasDatabaseName("idx_marker_assign_subject");

        builder.HasIndex(x => x.TeacherId)
            .HasDatabaseName("idx_marker_assign_teacher");
    }
}
