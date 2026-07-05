using IamService.Application.Features.Auth;
using IamService.Application.Interfaces;
using IAMService.API;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IamService.API.Controller
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _authService.LoginAsync(request.Email, request.Password);
            if (!result.Success)
            {
                return BadRequest(new ApiResponse<AuthResult>
                {
                    StatusCode = 400,
                    Message = string.Join(", ", result.Errors),
                    Data = result,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<AuthResult>
            {
                StatusCode = 200,
                Message = "Login successful",
                Data = result,
                ResponsedAt = DateTime.UtcNow
            });
        }

        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            var result = await _authService.GoogleLoginAsync(request.IdToken);
            if (!result.Success)
            {
                return BadRequest(new ApiResponse<AuthResult>
                {
                    StatusCode = 400,
                    Message = string.Join(", ", result.Errors),
                    Data = result,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<AuthResult>
            {
                StatusCode = 200,
                Message = "Google login successful",
                Data = result,
                ResponsedAt = DateTime.UtcNow
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request.AccessToken, request.RefreshToken);
            if (!result.Success)
            {
                return BadRequest(new ApiResponse<AuthResult>
                {
                    StatusCode = 400,
                    Message = string.Join(", ", result.Errors),
                    Data = result,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<AuthResult>
            {
                StatusCode = 200,
                Message = "Token refreshed successfully",
                Data = result,
                ResponsedAt = DateTime.UtcNow
            });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new ApiResponse<object>
                {
                    StatusCode = 401,
                    Message = "Unauthorized",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            var result = await _authService.LogoutAsync(userId);
            return Ok(new ApiResponse<AuthResult>
            {
                StatusCode = 200,
                Message = "Logout successful",
                Data = result,
                ResponsedAt = DateTime.UtcNow
            });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new ApiResponse<object>
                {
                    StatusCode = 401,
                    Message = "Unauthorized",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            var result = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            if (!result.Success)
            {
                return BadRequest(new ApiResponse<AuthResult>
                {
                    StatusCode = 400,
                    Message = string.Join(", ", result.Errors),
                    Data = result,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<AuthResult>
            {
                StatusCode = 200,
                Message = "Password changed successfully",
                Data = result,
                ResponsedAt = DateTime.UtcNow
            });
        }
    }
}
