using GradingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GradingService.Infrastructure.Persistence.Configurations;

public class LecturerMarkerCodeViewConfiguration : IEntityTypeConfiguration<LecturerMarkerCodeView>
{
    public void Configure(EntityTypeBuilder<LecturerMarkerCodeView> builder)
    {
        builder.ToTable("lecturer_marker_code_view");

        builder.HasKey(v => v.UserId);
        builder.Property(v => v.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever();

        builder.Property(v => v.MarkerCode)
            .HasColumnName("marker_code");

        builder.Property(v => v.FullName)
            .HasColumnName("full_name");

        builder.Property(v => v.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
