using System;
using System.Threading.Tasks;
using IamService.Application.Features.Users;

namespace IamService.Application.Interfaces
{
    public interface IUserService
    {
        Task<PagedResult<UserDto>> GetUsersAsync(int page, int pageSize, string? search);
        Task<UserDto?> GetUserByIdAsync(Guid id);
        Task<UserDto> CreateUserAsync(CreateUserRequest request, Guid adminId);
        Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserRequest request, Guid adminId);
        Task<bool> DeleteUserAsync(Guid id, Guid adminId);
    }
}
