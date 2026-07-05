using IamService.Application.Features.Auth;
using IamService.Application.Features.Users;
using IamService.Application.Interfaces;
using IamService.Domain.Entities;
using IamService.Domain.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IamService.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly ITokenService _tokenService;
        private readonly IUserRepository _userRepository;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly IRefreshTokenRepository _refreshTokenRepository;

        public AuthService(
            ITokenService tokenService,
            IUserRepository userRepository,
            IGoogleAuthService googleAuthService,
            IRefreshTokenRepository refreshTokenRepository)
        {
            _tokenService = tokenService;
            _userRepository = userRepository;
            _googleAuthService = googleAuthService;
            _refreshTokenRepository = refreshTokenRepository;
        }

        private async Task AuditLoginAsync(Guid? userId, string email, bool success, string ipAddress, string userAgent, string? reason = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = success ? "LoginSuccess" : "LoginFailed",
                EntityType = "Auth",
                EntityId = userId ?? Guid.Empty,
                NewValue = System.Text.Json.JsonSerializer.Serialize(new
                {
                    Email = email,
                    IP = ipAddress,
                    Device = userAgent,
                    Reason = reason
                })
            };
            await _userRepository.AddAuditLogAsync(auditLog);
        }

        public async Task<AuthResult> LoginAsync(string email, string password, string ipAddress, string userAgent)
        {
            var user = await _userRepository.FindByEmailAsync(email);
            if (user == null || user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                var authResult = new AuthResult { Success = false };
                authResult.Errors.Add("Invalid email or password");
                await AuditLoginAsync(null, email, false, ipAddress, userAgent, "Invalid email or password");
                return authResult;
            }

            if (user.Status != UserStatus.Active)
            {
                var authResult = new AuthResult { Success = false };
                authResult.Errors.Add("Account is not active");
                await AuditLoginAsync(user.Id, email, false, ipAddress, userAgent, "Account is not active");
                return authResult;
            }

            user.RecordLogin();
            await _userRepository.UpdateAsync(user);

            var tokens = await _tokenService.GenerateTokensAsync(user);
            await AuditLoginAsync(user.Id, email, true, ipAddress, userAgent);
            return tokens;
        }

        public async Task<AuthResult> GoogleLoginAsync(string idToken, string ipAddress, string userAgent)
        {
            GoogleUserInfo? googleUser;
            try
            {
                googleUser = await _googleAuthService.ValidateIdTokenAsync(idToken);
            }
            catch (InvalidOperationException ex)
            {
                var result = new AuthResult { Success = false };
                result.Errors.Add(ex.Message);
                await AuditLoginAsync(null, "GoogleTokenValidation", false, ipAddress, userAgent, ex.Message);
                return result;
            }

            if (googleUser == null)
            {
                var result = new AuthResult { Success = false };
                result.Errors.Add("Invalid Google token");
                await AuditLoginAsync(null, "GoogleTokenValidation", false, ipAddress, userAgent, "Invalid Google token");
                return result;
            }

            var user = await _userRepository.FindByGoogleIdAsync(googleUser.GoogleId)
                    ?? await _userRepository.FindByEmailAsync(googleUser.Email);

            if (user != null && user.Status != UserStatus.Active)
            {
                var result = new AuthResult { Success = false };
                result.Errors.Add("Account is not active");
                await AuditLoginAsync(user.Id, googleUser.Email, false, ipAddress, userAgent, "Account is not active");
                return result;
            }

            if (user == null)
            {
                // Auto-register new Google user
                user = new User
                {
                    Email = googleUser.Email,
                    FullName = googleUser.FullName,
                    GoogleId = googleUser.GoogleId,
                    AvatarUrl = googleUser.AvatarUrl,
                    LoginProvider = LoginProvider.Google,
                    Status = UserStatus.Active,
                    Role = UserRole.Lecturer
                };
                await _userRepository.AddAsync(user);
            }
            else if (user.GoogleId == null)
            {
                // Link existing account with Google
                user.GoogleId = googleUser.GoogleId;
                user.AvatarUrl = googleUser.AvatarUrl;
                user.LoginProvider = LoginProvider.Google;
                await _userRepository.UpdateAsync(user);
            }

            user.RecordLogin();
            await _userRepository.UpdateAsync(user);

            var tokens = await _tokenService.GenerateTokensAsync(user);
            await AuditLoginAsync(user.Id, googleUser.Email, true, ipAddress, userAgent);
            return tokens;
        }

        public async Task<AuthResult> RefreshTokenAsync(string token, string refreshToken)
        {
            return await _tokenService.VerifyAndGenerateTokenAsync(token, refreshToken);
        }

        public async Task<AuthResult> LogoutAsync(Guid userId)
        {
            await _refreshTokenRepository.RevokeAllByUserIdAsync(userId);
            return new AuthResult { Success = true };
        }

        public async Task<AuthResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            var result = new AuthResult { Success = false };

            var user = await _userRepository.FindByIdAsync(userId);
            if (user == null || user.IsDeleted)
            {
                result.Errors.Add("User not found");
                return result;
            }

            if (user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                result.Errors.Add("Mật khẩu hiện tại không chính xác.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                result.Errors.Add("Mật khẩu mới phải có ít nhất 8 ký tự.");
            }
            else
            {
                bool hasUpper = newPassword.Any(char.IsUpper);
                bool hasLower = newPassword.Any(char.IsLower);
                bool hasDigit = newPassword.Any(char.IsDigit);
                bool hasSpecial = newPassword.Any(c => !char.IsLetterOrDigit(c));

                if (!hasUpper || !hasLower || !hasDigit || !hasSpecial)
                {
                    result.Errors.Add("Mật khẩu mới phải chứa ít nhất 1 chữ hoa, 1 chữ thường, 1 chữ số và 1 ký tự đặc biệt.");
                }
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _userRepository.UpdateAsync(user);

            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = "ChangePassword",
                EntityType = "User",
                EntityId = userId,
                NewValue = "Password changed successfully"
            };
            await _userRepository.AddAuditLogAsync(auditLog);

            result.Success = true;
            return result;
        }

        public async Task<UserDto?> GetProfileAsync(Guid userId)
        {
            var user = await _userRepository.FindByIdAsync(userId);
            if (user == null || user.IsDeleted) return null;

            return MapToDto(user);
        }

        public async Task<UserDto?> UpdateProfileAsync(Guid userId, string fullName, string? phoneNumber, string? avatarUrl)
        {
            var user = await _userRepository.FindByIdAsync(userId);
            if (user == null || user.IsDeleted) return null;

            var oldState = new
            {
                user.FullName,
                user.PhoneNumber,
                user.AvatarUrl
            };

            user.FullName = fullName;
            user.PhoneNumber = phoneNumber;
            user.AvatarUrl = avatarUrl;

            await _userRepository.UpdateAsync(user);

            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = "UpdateProfile",
                EntityType = "User",
                EntityId = userId,
                OldValue = System.Text.Json.JsonSerializer.Serialize(oldState),
                NewValue = System.Text.Json.JsonSerializer.Serialize(new
                {
                    user.FullName,
                    user.PhoneNumber,
                    user.AvatarUrl
                })
            };
            await _userRepository.AddAuditLogAsync(auditLog);

            return MapToDto(user);
        }

        public async Task<string?> GeneratePasswordResetTokenAsync(string email)
        {
            var user = await _userRepository.FindByEmailAsync(email);
            if (user == null || user.IsDeleted) return null;

            var token = Guid.NewGuid().ToString("N");
            user.ResetToken = token;
            user.ResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);

            await _userRepository.UpdateAsync(user);

            var auditLog = new AuditLog
            {
                UserId = null,
                Action = "ForgotPasswordRequested",
                EntityType = "User",
                EntityId = user.Id,
                NewValue = System.Text.Json.JsonSerializer.Serialize(new { Email = email, ExpiresAt = user.ResetTokenExpiresAt })
            };
            await _userRepository.AddAuditLogAsync(auditLog);

            return token;
        }

        public async Task<AuthResult> ResetPasswordAsync(string token, string newPassword)
        {
            var result = new AuthResult { Success = false };

            if (string.IsNullOrWhiteSpace(token))
            {
                result.Errors.Add("Mã khôi phục không hợp lệ.");
                return result;
            }

            var user = await _userRepository.FindByResetTokenAsync(token);
            if (user == null || user.IsDeleted || user.ResetTokenExpiresAt < DateTime.UtcNow)
            {
                result.Errors.Add("Mã khôi phục không hợp lệ hoặc đã hết hạn.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                result.Errors.Add("Mật khẩu mới phải có ít nhất 8 ký tự.");
            }
            else
            {
                bool hasUpper = newPassword.Any(char.IsUpper);
                bool hasLower = newPassword.Any(char.IsLower);
                bool hasDigit = newPassword.Any(char.IsDigit);
                bool hasSpecial = newPassword.Any(c => !char.IsLetterOrDigit(c));

                if (!hasUpper || !hasLower || !hasDigit || !hasSpecial)
                {
                    result.Errors.Add("Mật khẩu mới phải chứa ít nhất 1 chữ hoa, 1 chữ thường, 1 chữ số và 1 ký tự đặc biệt.");
                }
            }

            if (result.Errors.Count > 0)
            {
                return result;
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.ResetToken = null;
            user.ResetTokenExpiresAt = null;

            await _userRepository.UpdateAsync(user);

            var auditLog = new AuditLog
            {
                UserId = user.Id,
                Action = "ResetPassword",
                EntityType = "User",
                EntityId = user.Id,
                NewValue = "Password reset successfully"
            };
            await _userRepository.AddAuditLogAsync(auditLog);

            result.Success = true;
            return result;
        }

        private static UserDto MapToDto(User user)
        {
            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                MarkerCode = user.MarkerCode,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role,
                Status = user.Status,
                LastLoginAt = user.LastLoginAt,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
    }
}
