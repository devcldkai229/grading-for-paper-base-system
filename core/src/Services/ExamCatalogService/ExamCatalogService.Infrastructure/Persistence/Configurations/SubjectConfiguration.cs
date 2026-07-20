using ExamCatalogService.Domain.Entities;
using ExamCatalogService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamCatalogService.Infrastructure.Persistence.Configurations;

public class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("subjects", table =>
        {
            table.HasCheckConstraint("CK_subjects_max_score", "max_score > 0");
        });

        builder.HasKey(subject => subject.Id);
        builder.Property(subject => subject.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(subject => subject.ExamId)
            .HasColumnName("exam_id")
            .IsRequired();

        builder.HasIndex(subject => subject.ExamId)
            .HasDatabaseName("idx_subjects_exam");

        builder.Property(subject => subject.SubjectCode)
            .HasColumnName("subject_code")
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(subject => new { subject.ExamId, subject.SubjectCode })
            .IsUnique();

        builder.Property(subject => subject.Title)
            .HasColumnName("title")
            .HasMaxLength(255);

        builder.Property(subject => subject.MaxScore)
            .HasColumnName("max_score")
            .HasColumnType("numeric(5,2)")
            .IsRequired()
            .HasDefaultValue(10m);

        builder.Property(subject => subject.PassScore)
            .HasColumnName("pass_score")
            .HasColumnType("numeric(5,2)");

        builder.Property(subject => subject.GradingDeadline)
            .HasColumnName("grading_deadline")
            .HasColumnType("date");

        builder.Property(subject => subject.ExamPaperS3Key)
            .HasColumnName("exam_paper_s3_key");

        builder.Property(subject => subject.ExamPaperFileName)
            .HasColumnName("exam_paper_file_name")
            .HasMaxLength(500);

        builder.Property(subject => subject.ExamPaperContentType)
            .HasColumnName("exam_paper_content_type")
            .HasMaxLength(120);

        builder.Property(subject => subject.ExamPaperPreviewS3Key)
            .HasColumnName("exam_paper_preview_s3_key");

        builder.Property(subject => subject.ExamPaperPreviewContentType)
            .HasColumnName("exam_paper_preview_content_type")
            .HasMaxLength(120);

        builder.Property(subject => subject.RubricS3Key)
            .HasColumnName("rubric_s3_key");

        builder.Property(subject => subject.RubricFileName)
            .HasColumnName("rubric_file_name")
            .HasMaxLength(500);

        builder.Property(subject => subject.RubricContentType)
            .HasColumnName("rubric_content_type")
            .HasMaxLength(120);

        builder.Property(subject => subject.RubricPreviewS3Key)
            .HasColumnName("rubric_preview_s3_key");

        builder.Property(subject => subject.RubricPreviewContentType)
            .HasColumnName("rubric_preview_content_type")
            .HasMaxLength(120);

        builder.Property(subject => subject.RubricVersion)
            .HasColumnName("rubric_version")
            .IsRequired()
            .HasDefaultValue(1);

        builder.Property(subject => subject.Status)
            .HasColumnName("status")
            .HasColumnType("subject_status")
            .IsRequired()
            .HasDefaultValue(SubjectStatus.Draft);

        builder.HasIndex(subject => subject.Status)
            .HasDatabaseName("idx_subjects_status");

        builder.Property(subject => subject.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(subject => subject.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasMany(subject => subject.Questions)
            .WithOne(question => question.Subject)
            .HasForeignKey(question => question.SubjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
