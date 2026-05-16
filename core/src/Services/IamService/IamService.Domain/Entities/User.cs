using Contracts.Domain;
using IamService.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace IamService.Domain.Entities
{
    public class User : Entity
    {
        public string Email { get; set; }

        public string? PasswordHash { get; set; }

        public string? GoogleId { get; set; }

        public string? AvatarUrl { get; set; }

        public string? FullName { get; set; }

        public string? PhoneNumber { get; set; }

        public LoginProvider LoginProvider { get; set; }  = LoginProvider.PasswordAuth;

        public UserStatus Status { get; set; }

        public UserRole Role { get; set; }

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

        public void Deactivate()
        {
            Status = UserStatus.Inactive;
            UpdatedAt = DateTime.UtcNow;
        }
    }


}
