using System.Threading.Tasks;

namespace IamService.Application.Interfaces
{
    public class GoogleUserInfo
    {
        public string GoogleId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? AvatarUrl { get; set; }
    }

    public interface IGoogleAuthService
    {
        Task<GoogleUserInfo?> ValidateIdTokenAsync(string idToken);
    }
}
