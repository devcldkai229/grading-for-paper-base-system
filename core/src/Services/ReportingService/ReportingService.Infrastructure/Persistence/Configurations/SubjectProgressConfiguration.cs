using ReportingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReportingService.Infrastructure.Persistence.Configurations;

public class SubjectProgressConfiguration : IEntityTypeConfiguration<SubjectProgress>
{
    public void Configure(EntityTypeBuilder<SubjectProgress> builder)
    {
        builder.ToTable("subject_progress");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Ignore(x => x.CreatedAt);

        builder.Property(x => x.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.Property(x => x.TotalPapers)
            .HasColumnName("total_papers")
            .IsRequired();

        builder.Property(x => x.CompletedPapers)
            .HasColumnName("completed_papers")
            .IsRequired();

        builder.Property(x => x.InProgress)
            .HasColumnName("in_progress")
            .IsRequired();

        builder.Property(x => x.NotStarted)
            .HasColumnName("not_started")
            .IsRequired();

        builder.Property(x => x.ScoreAvg)
            .HasColumnName("score_avg")
            .HasPrecision(5, 2);

        builder.Property(x => x.ScoreMin)
            .HasColumnName("score_min")
            .HasPrecision(5, 2);

        builder.Property(x => x.ScoreMax)
            .HasColumnName("score_max")
            .HasPrecision(5, 2);

        builder.Property(x => x.EstimatedFinish)
            .HasColumnName("estimated_finish");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(x => x.SubjectId)
            .IsUnique();
    }
}
