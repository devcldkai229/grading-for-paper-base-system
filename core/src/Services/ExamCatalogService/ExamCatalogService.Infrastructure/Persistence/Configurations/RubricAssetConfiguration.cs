using ExamCatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamCatalogService.Infrastructure.Persistence.Configurations;

public class RubricAssetConfiguration : IEntityTypeConfiguration<RubricAsset>
{
    public void Configure(EntityTypeBuilder<RubricAsset> builder)
    {
        builder.ToTable("rubric_assets");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Ignore(a => a.UpdatedAt);

        builder.Property(a => a.SubjectId).HasColumnName("subject_id").IsRequired();
        builder.Property(a => a.RubricVersion).HasColumnName("rubric_version").IsRequired();
        builder.Property(a => a.AssetId).HasColumnName("asset_id").HasMaxLength(64).IsRequired();
        builder.Property(a => a.Kind).HasColumnName("kind").HasMaxLength(32).IsRequired();
        builder.Property(a => a.Question).HasColumnName("question").HasMaxLength(32);
        builder.Property(a => a.PageIndex).HasColumnName("page_index").IsRequired();
        builder.Property(a => a.S3Key).HasColumnName("s3_key").IsRequired();
        builder.Property(a => a.ContentType).HasColumnName("content_type").HasMaxLength(64).IsRequired();
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(a => new { a.SubjectId, a.RubricVersion })
            .HasDatabaseName("idx_rubric_assets_subject_version");
    }
}
