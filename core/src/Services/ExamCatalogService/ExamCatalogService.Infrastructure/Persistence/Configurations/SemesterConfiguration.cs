using ExamCatalogService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExamCatalogService.Infrastructure.Persistence.Configurations;

public class SemesterConfiguration : IEntityTypeConfiguration<Semester>
{
    public void Configure(EntityTypeBuilder<Semester> builder)
    {
        builder.ToTable("semesters", table =>
        {
            table.HasCheckConstraint("chk_semester_dates", "end_date > start_date");
        });

        builder.HasKey(semester => semester.Id);
        builder.Property(semester => semester.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(semester => semester.Code)
            .HasColumnName("code")
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(semester => semester.Code)
            .IsUnique();

        builder.Property(semester => semester.Name)
            .HasColumnName("name")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(semester => semester.Description)
            .HasColumnName("description");

        builder.Property(semester => semester.StartDate)
            .HasColumnName("start_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(semester => semester.EndDate)
            .HasColumnName("end_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(semester => semester.IsActive)
            .HasColumnName("is_active")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(semester => semester.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(semester => semester.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasMany(semester => semester.Exams)
            .WithOne(exam => exam.Semester)
            .HasForeignKey(exam => exam.SemesterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
