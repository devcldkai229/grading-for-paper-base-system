using Contracts.Domain;

namespace IamService.Domain.Entities;

public class RefreshToken : Entity
{
    public string TokenHash { get; set; } = string.Empty;

    public string JwtId { get; set; } = string.Empty;

    public string? UserAgent { get; set; }

    public string? IpAddress { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public bool IsRevoked { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;
}
