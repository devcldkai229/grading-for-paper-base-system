using IamService.Application.Features.Auth;
using IamService.Application.Features.Users;
using IamService.Application.Interfaces;
using IAMService.API;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IamService.API.Controller
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IWebHostEnvironment _env;

        public AuthController(IAuthService authService, IWebHostEnvironment env)
        {
            _authService = authService;
            _env = env;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = Request.Headers["User-Agent"].ToString() ?? "Unknown";

            var result = await _authService.LoginAsync(request.Email, request.Password, ipAddress, userAgent);
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
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = Request.Headers["User-Agent"].ToString() ?? "Unknown";

            var result = await _authService.GoogleLoginAsync(request.IdToken, ipAddress, userAgent);
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

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
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

            var profile = await _authService.GetProfileAsync(userId);
            if (profile == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    StatusCode = 404,
                    Message = "Không tìm thấy hồ sơ người dùng",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<UserDto>
            {
                StatusCode = 200,
                Message = "Profile retrieved successfully",
                Data = profile,
                ResponsedAt = DateTime.UtcNow
            });
        }

        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
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

            if (string.IsNullOrWhiteSpace(request.FullName))
            {
                return BadRequest(new ApiResponse<object>
                {
                    StatusCode = 400,
                    Message = "Họ và tên không được để trống.",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            var updatedProfile = await _authService.UpdateProfileAsync(
                userId, 
                request.FullName.Trim(), 
                request.PhoneNumber?.Trim(), 
                request.AvatarUrl?.Trim()
            );

            if (updatedProfile == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    StatusCode = 404,
                    Message = "Không tìm thấy hồ sơ người dùng",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<UserDto>
            {
                StatusCode = 200,
                Message = "Profile updated successfully",
                Data = updatedProfile,
                ResponsedAt = DateTime.UtcNow
            });
        }

        [Authorize]
        [HttpPost("profile/avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ApiResponse<object>
                {
                    StatusCode = 400,
                    Message = "Không có file nào được tải lên.",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!System.Linq.Enumerable.Contains(allowedExtensions, extension))
            {
                return BadRequest(new ApiResponse<object>
                {
                    StatusCode = 400,
                    Message = "Định dạng file không hợp lệ. Chỉ cho phép các định dạng ảnh: .jpg, .jpeg, .png, .gif, .webp",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            if (file.Length > 2 * 1024 * 1024)
            {
                return BadRequest(new ApiResponse<object>
                {
                    StatusCode = 400,
                    Message = "Dung lượng ảnh tối đa là 2MB.",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            var uploadDir = Path.Combine(_env.ContentRootPath, "Uploads", "Avatars");
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            var uniqueFilename = Guid.NewGuid().ToString() + extension;
            var filePath = Path.Combine(uploadDir, uniqueFilename);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var avatarUrl = $"/auth/profile/avatar/{uniqueFilename}";
            return Ok(new ApiResponse<object>
            {
                StatusCode = 200,
                Message = "Tải ảnh đại diện lên thành công",
                Data = new { avatarUrl },
                ResponsedAt = DateTime.UtcNow
            });
        }

        [HttpGet("profile/avatar/{filename}")]
        public IActionResult GetAvatar(string filename)
        {
            var uploadDir = Path.Combine(_env.ContentRootPath, "Uploads", "Avatars");
            var filePath = Path.Combine(uploadDir, filename);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new ApiResponse<object>
                {
                    StatusCode = 404,
                    Message = "Không tìm thấy ảnh đại diện",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            var extension = Path.GetExtension(filename).ToLower();
            var contentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return base.File(fileBytes, contentType);
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new ApiResponse<object>
                {
                    StatusCode = 400,
                    Message = "Email không được để trống.",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            var token = await _authService.GeneratePasswordResetTokenAsync(request.Email.Trim());
            
            return Ok(new ApiResponse<object>
            {
                StatusCode = 200,
                Message = "Yêu cầu khôi phục mật khẩu đã được xử lý. Vui lòng kiểm tra email.",
                Data = new { resetToken = token },
                ResponsedAt = DateTime.UtcNow
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var result = await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
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
                Message = "Mật khẩu đã được đặt lại thành công.",
                Data = result,
                ResponsedAt = DateTime.UtcNow
            });
        }
    }
}
