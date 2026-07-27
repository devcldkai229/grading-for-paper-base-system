using ReportingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReportingService.Infrastructure.Persistence.Configurations;

public class ScoreRecordConfiguration : IEntityTypeConfiguration<ScoreRecord>
{
    public void Configure(EntityTypeBuilder<ScoreRecord> builder)
    {
        builder.ToTable("score_record");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(x => x.PaperId).HasColumnName("paper_id").IsRequired();
        builder.Property(x => x.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(x => x.TeacherId).HasColumnName("teacher_id").IsRequired();
        builder.Property(x => x.AliasNumber).HasColumnName("alias_number");
        builder.Property(x => x.TotalScore).HasColumnName("total_score").HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.MaxScore).HasColumnName("max_score").HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(x => x.PaperId).IsUnique();
        builder.HasIndex(x => x.SubjectId).HasDatabaseName("idx_score_record_subj");
    }
}
