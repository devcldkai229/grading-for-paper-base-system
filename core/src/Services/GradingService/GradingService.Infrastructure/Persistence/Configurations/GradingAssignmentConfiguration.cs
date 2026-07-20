using GradingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GradingService.Infrastructure.Persistence.Configurations;

public class GradingAssignmentConfiguration : IEntityTypeConfiguration<GradingAssignment>
{
    public void Configure(EntityTypeBuilder<GradingAssignment> builder)
    {
        builder.ToTable("grading_assignments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.StudentPaperId)
            .HasColumnName("student_paper_id")
            .IsRequired();

        builder.Property(x => x.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.Property(x => x.TeacherId)
            .HasColumnName("teacher_id")
            .IsRequired();

        builder.Property(x => x.AssignmentType)
            .HasColumnName("assignment_type")
            .HasColumnType("assignment_type");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("grading_progress_status");

        builder.Property(x => x.AiStatus)
            .HasColumnName("ai_status")
            .HasColumnType("ai_sync_status");

        builder.Property(x => x.IsFlagged)
            .HasColumnName("is_flagged")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(x => new { x.StudentPaperId, x.TeacherId, x.AssignmentType })
            .IsUnique();

        builder.HasIndex(x => x.StudentPaperId)
            .HasDatabaseName("idx_assign_paper");

        builder.HasIndex(x => x.TeacherId)
            .HasDatabaseName("idx_assign_teacher");

        builder.HasIndex(x => x.SubjectId)
            .HasDatabaseName("idx_assign_subject");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("idx_assign_status");

        builder.HasIndex(x => x.AiStatus)
            .HasDatabaseName("idx_assign_ai_status");

        builder.HasIndex(x => x.IsFlagged)
            .HasDatabaseName("idx_assign_flagged");
    }
}
