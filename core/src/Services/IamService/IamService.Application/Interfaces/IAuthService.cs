using IamService.Domain.Entities;
using System.Threading.Tasks;
using IamService.Application.Features.Auth;
using IamService.Application.Features.Users;

namespace IamService.Application.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResult> LoginAsync(string email, string password);
        Task<AuthResult> GoogleLoginAsync(string idToken);
        Task<AuthResult> RefreshTokenAsync(string token, string refreshToken);
        Task<AuthResult> LogoutAsync(System.Guid userId);
        Task<AuthResult> ChangePasswordAsync(System.Guid userId, string currentPassword, string newPassword);
        Task<UserDto?> GetProfileAsync(System.Guid userId);
        Task<UserDto?> UpdateProfileAsync(System.Guid userId, string fullName, string? phoneNumber, string? avatarUrl);
    }
}
