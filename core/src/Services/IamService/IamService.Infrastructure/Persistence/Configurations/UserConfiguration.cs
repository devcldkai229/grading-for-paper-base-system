using IamService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IamService.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.Property(user => user.PasswordHash)
            .HasMaxLength(512);

        builder.Property(user => user.GoogleId)
            .HasMaxLength(128);

        builder.Property(user => user.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(user => user.FullName)
            .HasMaxLength(200);

        builder.Property(user => user.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(user => user.LoginProvider)
            .HasConversion<int>();

        builder.Property(user => user.Status)
            .HasConversion<int>();

        builder.Property(user => user.Role)
            .HasConversion<int>();

        builder.Property(user => user.CreatedAt).IsRequired();
    }
}