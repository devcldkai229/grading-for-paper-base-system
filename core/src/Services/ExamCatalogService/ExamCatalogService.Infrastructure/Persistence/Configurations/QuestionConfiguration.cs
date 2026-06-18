using ExamCatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamCatalogService.Infrastructure.Persistence.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions", table =>
        {
            table.HasCheckConstraint("CK_questions_max_score", "max_score > 0");
        });

        builder.HasKey(question => question.Id);
        builder.Property(question => question.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Ignore(question => question.UpdatedAt);

        builder.Property(question => question.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.HasIndex(question => question.SubjectId)
            .HasDatabaseName("idx_questions_subject");

        builder.Property(question => question.QuestionNumber)
            .HasColumnName("question_number")
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(question => new { question.SubjectId, question.QuestionNumber })
            .IsUnique();

        builder.Property(question => question.Label)
            .HasColumnName("label")
            .HasMaxLength(255);

        builder.Property(question => question.MaxScore)
            .HasColumnName("max_score")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(question => question.OrderIndex)
            .HasColumnName("order_index")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(question => question.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();
    }
}
