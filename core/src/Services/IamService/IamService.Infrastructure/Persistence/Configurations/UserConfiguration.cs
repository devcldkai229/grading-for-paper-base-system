using IamService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IamService.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(255);

        builder.Property(user => user.GoogleId)
            .HasColumnName("google_id")
            .HasMaxLength(128);

        builder.Property(user => user.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(500);

        builder.Property(user => user.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(255);

        builder.Property(user => user.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(50);

        builder.Property(user => user.MarkerCode)
            .HasColumnName("marker_code")
            .HasMaxLength(50);

        builder.Property(user => user.LoginProvider)
            .HasColumnName("login_provider")
            .HasColumnType("login_provider");

        builder.Property(user => user.Status)
            .HasColumnName("status")
            .HasColumnType("user_status");

        builder.Property(user => user.Role)
            .HasColumnName("role")
            .HasColumnType("user_role");

        builder.Property(user => user.LastLoginAt)
            .HasColumnName("last_login_at");

        builder.Property(user => user.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(user => user.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(user => user.Role)
            .HasDatabaseName("idx_users_role");

        builder.HasIndex(user => user.Status)
            .HasDatabaseName("idx_users_status");
    }
}
