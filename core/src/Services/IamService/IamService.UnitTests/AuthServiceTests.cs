using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IamService.Application.Features.Auth;
using IamService.Application.Interfaces;
using IamService.Application.Services;
using IamService.Domain.Entities;
using IamService.Domain.Enums;
using NSubstitute;
using Xunit;

namespace IamService.UnitTests
{
    public class AuthServiceTests
    {
        private readonly ITokenService _tokenService;
        private readonly IUserRepository _userRepository;
        private readonly IGoogleAuthService _googleAuthService;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly AuthService _authService;

        public AuthServiceTests()
        {
            _tokenService = Substitute.For<ITokenService>();
            _userRepository = Substitute.For<IUserRepository>();
            _googleAuthService = Substitute.For<IGoogleAuthService>();
            _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();

            _authService = new AuthService(
                _tokenService,
                _userRepository,
                _googleAuthService,
                _refreshTokenRepository
            );
        }

        [Fact]
        public async Task LoginAsync_WithValidCredentials_ShouldAuditSuccessAndReturnTokens()
        {
            // Arrange
            var email = "test@fpt.edu.vn";
            var password = "PlainPassword123!";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User
            {
                Email = email,
                PasswordHash = passwordHash,
                Status = UserStatus.Active
            };

            _userRepository.FindByEmailAsync(email).Returns(user);
            _tokenService.GenerateTokensAsync(user).Returns(new AuthResult { Success = true, AccessToken = "valid_token" });

            // Act
            var result = await _authService.LoginAsync(email, password, "127.0.0.1", "Mozilla/5.0");

            // Assert
            Assert.True(result.Success);
            Assert.Equal("valid_token", result.AccessToken);
            await _userRepository.Received(1).AddAuditLogAsync(Arg.Is<AuditLog>(l => 
                l.Action == "LoginSuccess" && 
                l.EntityType == "Auth" && 
                l.EntityId == user.Id &&
                l.NewValue.Contains("127.0.0.1") &&
                l.NewValue.Contains("Mozilla/5.0")
            ));
        }

        [Fact]
        public async Task LoginAsync_WithInvalidPassword_ShouldAuditFailure()
        {
            // Arrange
            var email = "test@fpt.edu.vn";
            var password = "PlainPassword123!";
            var wrongPassword = "WrongPassword!";
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User
            {
                Email = email,
                PasswordHash = passwordHash,
                Status = UserStatus.Active
            };

            _userRepository.FindByEmailAsync(email).Returns(user);

            // Act
            var result = await _authService.LoginAsync(email, wrongPassword, "127.0.0.1", "Mozilla/5.0");

            // Assert
            Assert.False(result.Success);
            await _userRepository.Received(1).AddAuditLogAsync(Arg.Is<AuditLog>(l => 
                l.Action == "LoginFailed" && 
                l.EntityType == "Auth" && 
                l.EntityId == Guid.Empty &&
                l.NewValue.Contains("Invalid email or password")
            ));
        }

        [Fact]
        public async Task GeneratePasswordResetTokenAsync_ShouldGenerateTokenAndAudit()
        {
            // Arrange
            var email = "test@fpt.edu.vn";
            var user = new User
            {
                Email = email,
                Status = UserStatus.Active
            };

            _userRepository.FindByEmailAsync(email).Returns(user);

            // Act
            var token = await _authService.GeneratePasswordResetTokenAsync(email);

            // Assert
            Assert.NotNull(token);
            Assert.NotEmpty(token);
            Assert.Equal(token, user.ResetToken);
            Assert.NotNull(user.ResetTokenExpiresAt);
            Assert.True(user.ResetTokenExpiresAt > DateTime.UtcNow);

            await _userRepository.Received(1).AddAuditLogAsync(Arg.Is<AuditLog>(l => 
                l.Action == "ForgotPasswordRequested" && 
                l.EntityId == user.Id && 
                l.NewValue.Contains(email)
            ));
            await _userRepository.Received(1).UpdateAsync(user);
        }

        [Fact]
        public async Task ResetPasswordAsync_WithValidTokenAndStrongPassword_ShouldResetPasswordAndInvalidateToken()
        {
            // Arrange
            var token = "some_reset_token";
            var newPassword = "NewStrongPassword123!";
            var user = new User
            {
                Email = "test@fpt.edu.vn",
                ResetToken = token,
                ResetTokenExpiresAt = DateTime.UtcNow.AddHours(1),
                Status = UserStatus.Active
            };

            _userRepository.FindByResetTokenAsync(token).Returns(user);

            // Act
            var result = await _authService.ResetPasswordAsync(token, newPassword);

            // Assert
            Assert.True(result.Success);
            Assert.Null(user.ResetToken);
            Assert.Null(user.ResetTokenExpiresAt);
            Assert.True(BCrypt.Net.BCrypt.Verify(newPassword, user.PasswordHash!));

            await _userRepository.Received(1).UpdateAsync(user);
            await _userRepository.Received(1).AddAuditLogAsync(Arg.Is<AuditLog>(l => 
                l.Action == "ResetPassword" && 
                l.EntityId == user.Id
            ));
        }

        [Fact]
        public async Task ResetPasswordAsync_WithWeakPassword_ShouldFailValidation()
        {
            // Arrange
            var token = "some_reset_token";
            var newPassword = "weak"; // missing length, uppercase, numbers, special characters
            var user = new User
            {
                Email = "test@fpt.edu.vn",
                ResetToken = token,
                ResetTokenExpiresAt = DateTime.UtcNow.AddHours(1),
                Status = UserStatus.Active
            };

            _userRepository.FindByResetTokenAsync(token).Returns(user);

            // Act
            var result = await _authService.ResetPasswordAsync(token, newPassword);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Mật khẩu mới phải có ít nhất 8 ký tự.", result.Errors);
            await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>());
        }

        [Fact]
        public async Task ResetPasswordAsync_WithExpiredToken_ShouldFail()
        {
            // Arrange
            var token = "some_reset_token";
            var newPassword = "NewStrongPassword123!";
            var user = new User
            {
                Email = "test@fpt.edu.vn",
                ResetToken = token,
                ResetTokenExpiresAt = DateTime.UtcNow.AddHours(-1), // Expired
                Status = UserStatus.Active
            };

            _userRepository.FindByResetTokenAsync(token).Returns(user);

            // Act
            var result = await _authService.ResetPasswordAsync(token, newPassword);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Mã khôi phục không hợp lệ hoặc đã hết hạn.", result.Errors);
            await _userRepository.DidNotReceive().UpdateAsync(Arg.Any<User>());
        }
    }
}
