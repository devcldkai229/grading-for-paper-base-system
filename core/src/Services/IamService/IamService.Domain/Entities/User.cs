using Contracts.Domain;
using IamService.Domain.Enums;

namespace IamService.Domain.Entities;

public class User : Entity
{
    public string Email { get; set; } = string.Empty;

    public string? PasswordHash { get; set; }

    public string? GoogleId { get; set; }

    public string? AvatarUrl { get; set; }

    public string? FullName { get; set; }

    public string? PhoneNumber { get; set; }

    /// <summary>Alias hiển thị của giáo viên trong file điểm (vd: HungLD5, LamNN15).</summary>
    public string? MarkerCode { get; set; }

    public LoginProvider LoginProvider { get; set; } = LoginProvider.PasswordAuth;

    public UserStatus Status { get; set; }

    public UserRole Role { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public void Deactivate()
    {
        Status = UserStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        Status = UserStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Lock()
    {
        Status = UserStatus.Locked;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        Status = UserStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
