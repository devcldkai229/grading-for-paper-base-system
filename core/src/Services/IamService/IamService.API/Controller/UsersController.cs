using IamService.Application.Features.Users;
using IamService.Application.Interfaces;
using IAMService.API;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IamService.API.Controller
{
    [Route("api/users")]
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var result = await _userService.GetUsersAsync(page, pageSize, search);

            return Ok(new ApiResponse<PagedResult<UserDto>>
            {
                StatusCode = 200,
                Message = "Users retrieved successfully",
                Data = result,
                ResponsedAt = DateTime.UtcNow
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetUserById(Guid id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    StatusCode = 404,
                    Message = "User not found",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<UserDto>
            {
                StatusCode = 200,
                Message = "User retrieved successfully",
                Data = user,
                ResponsedAt = DateTime.UtcNow
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            var adminIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid adminId = Guid.Empty;
            if (!string.IsNullOrEmpty(adminIdStr))
            {
                Guid.TryParse(adminIdStr, out adminId);
            }

            try
            {
                var user = await _userService.CreateUserAsync(request, adminId);
                return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, new ApiResponse<UserDto>
                {
                    StatusCode = 201,
                    Message = "User created successfully",
                    Data = user,
                    ResponsedAt = DateTime.UtcNow
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<object>
                {
                    StatusCode = 400,
                    Message = ex.Message,
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
        {
            var adminIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid adminId = Guid.Empty;
            if (!string.IsNullOrEmpty(adminIdStr))
            {
                Guid.TryParse(adminIdStr, out adminId);
            }

            var user = await _userService.UpdateUserAsync(id, request, adminId);
            if (user == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    StatusCode = 404,
                    Message = "User not found",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<UserDto>
            {
                StatusCode = 200,
                Message = "User updated successfully",
                Data = user,
                ResponsedAt = DateTime.UtcNow
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var adminIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid adminId = Guid.Empty;
            if (!string.IsNullOrEmpty(adminIdStr))
            {
                Guid.TryParse(adminIdStr, out adminId);
            }

            var success = await _userService.DeleteUserAsync(id, adminId);
            if (!success)
            {
                return NotFound(new ApiResponse<object>
                {
                    StatusCode = 404,
                    Message = "User not found",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<object>
            {
                StatusCode = 200,
                Message = "User deleted successfully",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }
    }
}
