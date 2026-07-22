using ExamCatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamCatalogService.Infrastructure.Persistence.Configurations;

public class GradingContractConfiguration : IEntityTypeConfiguration<GradingContract>
{
    public void Configure(EntityTypeBuilder<GradingContract> builder)
    {
        builder.ToTable("grading_contracts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(c => c.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(c => c.RubricVersion).HasColumnName("rubric_version").IsRequired();
        builder.Property(c => c.ContractJson).HasColumnName("contract_json")
            .HasColumnType("jsonb").IsRequired();
        builder.Property(c => c.CoverageOk).HasColumnName("coverage_ok").IsRequired().HasDefaultValue(false);
        builder.Property(c => c.ModelUsed).HasColumnName("model_used");
        builder.Property(c => c.Status).HasColumnName("status")
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.ReviewedBy).HasColumnName("reviewed_by");
        builder.Property(c => c.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        // One contract per subject + rubric version.
        builder.HasIndex(c => new { c.SubjectId, c.RubricVersion })
            .IsUnique()
            .HasDatabaseName("idx_grading_contracts_subject_version");
    }
}
