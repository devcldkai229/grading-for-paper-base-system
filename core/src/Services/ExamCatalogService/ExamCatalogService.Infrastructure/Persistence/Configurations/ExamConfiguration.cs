using ExamCatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamCatalogService.Infrastructure.Persistence.Configurations;

public class ExamConfiguration : IEntityTypeConfiguration<Exam>
{
    public void Configure(EntityTypeBuilder<Exam> builder)
    {
        builder.ToTable("exams");

        builder.HasKey(exam => exam.Id);
        builder.Property(exam => exam.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(exam => exam.SemesterId)
            .HasColumnName("semester_id")
            .IsRequired();

        builder.HasIndex(exam => exam.SemesterId)
            .HasDatabaseName("idx_exams_semester");

        builder.Property(exam => exam.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(exam => exam.ExamType)
            .HasColumnName("exam_type")
            .HasColumnType("exam_type")
            .IsRequired()
            .HasDefaultValue(Domain.Enums.ExamType.FE);

        builder.Property(exam => exam.StartDate)
            .HasColumnName("start_date")
            .HasColumnType("date");

        builder.Property(exam => exam.EndDate)
            .HasColumnName("end_date")
            .HasColumnType("date");

        builder.Property(exam => exam.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(exam => exam.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasMany(exam => exam.Subjects)
            .WithOne(subject => subject.Exam)
            .HasForeignKey(subject => subject.ExamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
