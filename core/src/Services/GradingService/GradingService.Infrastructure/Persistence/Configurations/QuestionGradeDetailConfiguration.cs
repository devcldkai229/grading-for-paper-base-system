using GradingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GradingService.Infrastructure.Persistence.Configurations;

public class QuestionGradeDetailConfiguration : IEntityTypeConfiguration<QuestionGradeDetail>
{
    public void Configure(EntityTypeBuilder<QuestionGradeDetail> builder)
    {
        builder.ToTable("question_grade_details", t =>
        {
            t.HasCheckConstraint("CK_question_grade_details_score_range", "score >= 0 AND score <= max_score");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.GradingFormId)
            .HasColumnName("grading_form_id")
            .IsRequired();

        builder.Property(x => x.QuestionNumber)
            .HasColumnName("question_number")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.GroupLabel)
            .HasColumnName("group_label")
            .HasMaxLength(100);

        builder.Property(x => x.Label)
            .HasColumnName("label")
            .HasMaxLength(255);

        builder.Property(x => x.OrderIndex)
            .HasColumnName("order_index")
            .IsRequired();

        builder.Property(x => x.Score)
            .HasColumnName("score")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(x => x.MaxScore)
            .HasColumnName("max_score")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(x => x.QuestionComment)
            .HasColumnName("question_comment");

        builder.Property(x => x.AiDrafted)
            .HasColumnName("ai_drafted")
            .IsRequired();

        builder.Property(x => x.AiReviewStatus)
            .HasColumnName("ai_review_status")
            .HasColumnType("ai_review_status");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(x => new { x.GradingFormId, x.QuestionNumber })
            .IsUnique();

        builder.HasIndex(x => x.GradingFormId)
            .HasDatabaseName("idx_grade_details_form");

        builder.HasOne(x => x.GradingForm)
            .WithMany(x => x.QuestionGradeDetails)
            .HasForeignKey(x => x.GradingFormId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
