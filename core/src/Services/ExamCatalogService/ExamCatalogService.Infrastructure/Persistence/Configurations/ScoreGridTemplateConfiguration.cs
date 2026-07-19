using ExamCatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamCatalogService.Infrastructure.Persistence.Configurations;

public class ScoreGridTemplateConfiguration : IEntityTypeConfiguration<ScoreGridTemplate>
{
    public void Configure(EntityTypeBuilder<ScoreGridTemplate> builder)
    {
        builder.ToTable("score_grid_templates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Ignore(t => t.UpdatedAt);

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(t => t.Name)
            .HasDatabaseName("idx_score_grid_templates_name");

        builder.Property(t => t.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(t => t.QuestionsJson)
            .HasColumnName("questions_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(t => t.QuestionCount)
            .HasColumnName("question_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
