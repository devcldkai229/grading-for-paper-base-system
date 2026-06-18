using ReportingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReportingService.Infrastructure.Persistence.Configurations;

public class MarkerProgressConfiguration : IEntityTypeConfiguration<MarkerProgress>
{
    public void Configure(EntityTypeBuilder<MarkerProgress> builder)
    {
        builder.ToTable("marker_progress");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Ignore(x => x.CreatedAt);

        builder.Property(x => x.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.Property(x => x.TeacherId)
            .HasColumnName("teacher_id")
            .IsRequired();

        builder.Property(x => x.AssignedCount)
            .HasColumnName("assigned_count")
            .IsRequired();

        builder.Property(x => x.CompletedCount)
            .HasColumnName("completed_count")
            .IsRequired();

        builder.Property(x => x.DraftingCount)
            .HasColumnName("drafting_count")
            .IsRequired();

        builder.Property(x => x.AvgScore)
            .HasColumnName("avg_score")
            .HasPrecision(5, 2);

        builder.Property(x => x.ThroughputPerHour)
            .HasColumnName("throughput_per_hour")
            .HasPrecision(6, 2);

        builder.Property(x => x.LastActivityAt)
            .HasColumnName("last_activity_at");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => new { x.SubjectId, x.TeacherId })
            .IsUnique();

        builder.HasIndex(x => x.SubjectId)
            .HasDatabaseName("idx_marker_progress_subj");

        builder.HasIndex(x => x.TeacherId)
            .HasDatabaseName("idx_marker_progress_tea");
    }
}
