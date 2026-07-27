using Google.Apis.Auth;
using IamService.Application.Interfaces;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IamService.Infrastructure.Services
{
    public class GoogleAuthSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string[] AllowedDomains { get; set; } = Array.Empty<string>();
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

                // SSO domain restriction: Only allow school email domains
                if (_settings.AllowedDomains != null && _settings.AllowedDomains.Length > 0)
                {
                    var email = payload.Email;
                    var parts = email.Split('@');
                    if (parts.Length != 2 || !_settings.AllowedDomains.Contains(parts[1], StringComparer.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Đăng nhập Google thất bại: Chỉ cho phép tài khoản thuộc tên miền trường học ({string.Join(", ", _settings.AllowedDomains)}).");
                    }
                }

                return new GoogleUserInfo
                {
                    GoogleId = payload.Subject,
                    Email = payload.Email,
                    FullName = payload.Name,
                    AvatarUrl = payload.Picture
                };
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }
    }
}
