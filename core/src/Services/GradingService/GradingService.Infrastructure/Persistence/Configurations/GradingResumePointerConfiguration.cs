using GradingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GradingService.Infrastructure.Persistence.Configurations;

public class GradingResumePointerConfiguration : IEntityTypeConfiguration<GradingResumePointer>
{
    public void Configure(EntityTypeBuilder<GradingResumePointer> builder)
    {
        builder.ToTable("grading_resume_pointers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.TeacherId)
            .HasColumnName("teacher_id")
            .IsRequired();

        builder.Property(x => x.BatchId)
            .HasColumnName("batch_id")
            .IsRequired();

        builder.Property(x => x.AssignmentId)
            .HasColumnName("assignment_id")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(x => new { x.TeacherId, x.BatchId })
            .IsUnique()
            .HasDatabaseName("IX_grading_resume_pointers_teacher_id_batch_id");
    }
}
