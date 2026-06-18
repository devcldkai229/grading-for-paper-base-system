using ReportingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ReportingService.Infrastructure.Persistence.Configurations;

public class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.ToTable("export_jobs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Ignore(x => x.CreatedAt);
        builder.Ignore(x => x.UpdatedAt);

        builder.Property(x => x.SubjectId)
            .HasColumnName("subject_id")
            .IsRequired();

        builder.Property(x => x.RequestedBy)
            .HasColumnName("requested_by")
            .IsRequired();

        builder.Property(x => x.ResultS3Key)
            .HasColumnName("result_s3_key");

        builder.Property(x => x.ResultFileName)
            .HasColumnName("result_file_name")
            .HasMaxLength(500);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("export_status")
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message");

        builder.Property(x => x.RequestedAt)
            .HasColumnName("requested_at")
            .IsRequired();

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at");

        builder.HasIndex(x => x.SubjectId)
            .HasDatabaseName("idx_export_jobs_subject");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("idx_export_jobs_status");
    }
}
