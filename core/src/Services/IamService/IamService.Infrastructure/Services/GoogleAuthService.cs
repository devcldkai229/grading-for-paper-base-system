using Google.Apis.Auth;
using IamService.Application.Interfaces;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace IamService.Infrastructure.Services
{
    public class GoogleAuthSettings
    {
        public string ClientId { get; set; } = string.Empty;
    }

    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly GoogleAuthSettings _settings;

        public GoogleAuthService(IOptions<GoogleAuthSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task<GoogleUserInfo?> ValidateIdTokenAsync(string idToken)
        {
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings()
                {
                    Audience = new[] { _settings.ClientId }
                };

                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
                return new GoogleUserInfo
                {
                    GoogleId = payload.Subject,
                    Email = payload.Email,
                    FullName = payload.Name,
                    AvatarUrl = payload.Picture
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
