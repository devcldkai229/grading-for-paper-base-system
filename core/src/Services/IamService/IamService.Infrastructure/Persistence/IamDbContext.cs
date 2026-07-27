using IamService.Domain.Entities;
using IamService.Domain.Enums;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace IamService.Infrastructure.Persistence;

public class IamDbContext : DbContext
{
    public IamDbContext(DbContextOptions<IamDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<LoginProvider>(name: "login_provider");
        modelBuilder.HasPostgresEnum<UserStatus>(name: "user_status");
        modelBuilder.HasPostgresEnum<UserRole>(name: "user_role");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IamDbContext).Assembly);

        // MassTransit transactional outbox tables so LecturerProfileChanged is published atomically
        // with the user change (N7 — no dual-write).
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        base.OnModelCreating(modelBuilder);
    }
}
