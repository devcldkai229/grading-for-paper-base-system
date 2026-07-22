using ReportingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReportingService.Infrastructure.Persistence.Configurations;

public class AssignmentProgressConfiguration : IEntityTypeConfiguration<AssignmentProgress>
{
    public void Configure(EntityTypeBuilder<AssignmentProgress> builder)
    {
        builder.ToTable("assignment_progress");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(x => x.AssignmentId).HasColumnName("assignment_id").IsRequired();
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(x => x.TeacherId).HasColumnName("teacher_id").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(32).IsRequired();
        builder.Property(x => x.AliasNumber).HasColumnName("alias_number");
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.AssignmentId).IsUnique();
        builder.HasIndex(x => x.SubjectId).HasDatabaseName("idx_assignment_progress_subj");
        builder.HasIndex(x => new { x.SubjectId, x.TeacherId })
            .HasDatabaseName("idx_assignment_progress_subj_tea");
    }
}
