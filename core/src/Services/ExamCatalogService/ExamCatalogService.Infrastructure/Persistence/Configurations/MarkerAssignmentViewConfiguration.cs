using ExamCatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamCatalogService.Infrastructure.Persistence.Configurations;

public class MarkerAssignmentViewConfiguration : IEntityTypeConfiguration<MarkerAssignmentView>
{
    public void Configure(EntityTypeBuilder<MarkerAssignmentView> builder)
    {
        builder.ToTable("marker_assignment_view");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(v => v.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.Property(v => v.TeacherId)
            .HasColumnName("teacher_id")
            .IsRequired();

        builder.Property(v => v.AliasStart)
            .HasColumnName("alias_start")
            .IsRequired();

        builder.Property(v => v.AliasEnd)
            .HasColumnName("alias_end")
            .IsRequired();

        builder.Property(v => v.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Search scopes by lecturer; keep subject lookups cheap too.
        builder.HasIndex(v => v.TeacherId);
        builder.HasIndex(v => v.SubjectId);
    }
}
