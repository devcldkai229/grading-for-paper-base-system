using GradingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GradingService.Infrastructure.Persistence.Configurations;

public class GradingFormConfiguration : IEntityTypeConfiguration<GradingForm>
{
    public void Configure(EntityTypeBuilder<GradingForm> builder)
    {
        builder.ToTable("grading_forms");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.GradingAssignmentId)
            .HasColumnName("grading_assignment_id")
            .IsRequired();

        builder.Property(x => x.TotalScore)
            .HasColumnName("total_score")
            .HasColumnType("numeric(5,2)")
            .IsRequired();

        builder.Property(x => x.PaperComment)
            .HasColumnName("paper_comment");

        builder.Property(x => x.GeneralComment)
            .HasColumnName("general_comment");

        builder.Property(x => x.InternalComment)
            .HasColumnName("internal_comment");

        builder.Property(x => x.SubmittedAt)
            .HasColumnName("submitted_at");

        builder.Property(x => x.RowVersion)
            .HasColumnName("row_version")
            .HasDefaultValue(0)
            .IsConcurrencyToken()
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(x => x.GradingAssignmentId)
            .IsUnique();

        builder.HasOne(x => x.GradingAssignment)
            .WithOne(x => x.GradingForm)
            .HasForeignKey<GradingForm>(x => x.GradingAssignmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
