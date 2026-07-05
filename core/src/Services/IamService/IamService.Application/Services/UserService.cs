using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using IamService.Application.Features.Users;
using IamService.Application.Interfaces;
using IamService.Domain.Entities;
using IamService.Domain.Enums;

namespace IamService.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;

        public UserService(IUserRepository userRepository, IRefreshTokenRepository refreshTokenRepository)
        {
            _userRepository = userRepository;
            _refreshTokenRepository = refreshTokenRepository;
        }

        public async Task<PagedResult<UserDto>> GetUsersAsync(int page, int pageSize, string? search)
        {
            var (items, totalCount) = await _userRepository.GetUsersPagedAsync(page, pageSize, search);

            var dtos = items.Select(MapToDto);

            return new PagedResult<UserDto>
            {
                Items = dtos,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount
            };
        }

        public async Task<UserDto?> GetUserByIdAsync(Guid id)
        {
            var user = await _userRepository.FindByIdAsync(id);
            if (user == null || user.IsDeleted) return null;

            return MapToDto(user);
        }

        public async Task<UserDto> CreateUserAsync(CreateUserRequest request, Guid adminId)
        {
            var existingUser = await _userRepository.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                throw new InvalidOperationException("Email is already in use.");
            }

            var user = new User
            {
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                MarkerCode = request.MarkerCode,
                Role = request.Role,
                Status = UserStatus.Active,
                LoginProvider = LoginProvider.PasswordAuth
            };

            await _userRepository.AddAsync(user);

            var auditLog = new AuditLog
            {
                UserId = adminId,
                Action = "Create",
                EntityType = "User",
                EntityId = user.Id,
                NewValue = JsonSerializer.Serialize(new 
                { 
                    user.Email, 
                    user.FullName, 
                    user.PhoneNumber, 
                    user.MarkerCode, 
                    user.Role, 
                    user.Status 
                })
            };
            await _userRepository.AddAuditLogAsync(auditLog);

            return MapToDto(user);
        }

        public async Task<UserDto?> UpdateUserAsync(Guid id, UpdateUserRequest request, Guid adminId)
        {
            var user = await _userRepository.FindByIdAsync(id);
            if (user == null || user.IsDeleted) return null;

            var oldState = new 
            { 
                user.Email, 
                user.FullName, 
                user.PhoneNumber, 
                user.MarkerCode, 
                user.Role, 
                user.Status 
            };

            if (request.FullName != null) user.FullName = request.FullName;
            if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;
            if (request.MarkerCode != null) user.MarkerCode = request.MarkerCode;
            if (request.Role.HasValue) user.Role = request.Role.Value;
            
            if (request.Status.HasValue)
            {
                switch (request.Status.Value)
                {
                    case UserStatus.Active:
                        user.Activate();
                        break;
                    case UserStatus.Inactive:
                        user.Deactivate();
                        break;
                    case UserStatus.Locked:
                        user.Lock();
                        break;
                }
            }

            await _userRepository.UpdateAsync(user);

            if (user.Status != UserStatus.Active)
            {
                await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id);
            }

            var auditLog = new AuditLog
            {
                UserId = adminId,
                Action = "Update",
                EntityType = "User",
                EntityId = user.Id,
                OldValue = JsonSerializer.Serialize(oldState),
                NewValue = JsonSerializer.Serialize(new 
                { 
                    user.Email, 
                    user.FullName, 
                    user.PhoneNumber, 
                    user.MarkerCode, 
                    user.Role, 
                    user.Status 
                })
            };
            await _userRepository.AddAuditLogAsync(auditLog);

            return MapToDto(user);
        }

        public async Task<bool> DeleteUserAsync(Guid id, Guid adminId)
        {
            var user = await _userRepository.FindByIdAsync(id);
            if (user == null || user.IsDeleted) return false;

            var oldState = new 
            { 
                user.Email, 
                user.FullName, 
                user.PhoneNumber, 
                user.MarkerCode, 
                user.Role, 
                user.Status 
            };

            var hasData = await _userRepository.HasAssociatedDataAsync(id);

            if (hasData)
            {
                user.SoftDelete();
                await _userRepository.UpdateAsync(user);
                await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id);

                var auditLog = new AuditLog
                {
                    UserId = adminId,
                    Action = "SoftDelete",
                    EntityType = "User",
                    EntityId = user.Id,
                    OldValue = JsonSerializer.Serialize(oldState),
                    NewValue = JsonSerializer.Serialize(new 
                    { 
                        user.Email, 
                        user.FullName, 
                        user.PhoneNumber, 
                        user.MarkerCode, 
                        user.Role, 
                        user.Status,
                        user.IsDeleted
                    })
                };
                await _userRepository.AddAuditLogAsync(auditLog);
            }
            else
            {
                await _userRepository.DeleteAsync(user);
                await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id);

                var auditLog = new AuditLog
                {
                    UserId = adminId,
                    Action = "HardDelete",
                    EntityType = "User",
                    EntityId = user.Id,
                    OldValue = JsonSerializer.Serialize(oldState)
                };
                await _userRepository.AddAuditLogAsync(auditLog);
            }

            return true;
        }

        public async Task<PagedResult<AuditLogDto>> GetAuditLogsAsync(
            int page, 
            int pageSize, 
            Guid? userId, 
            string? action, 
            string? entityType, 
            DateTime? startDate, 
            DateTime? endDate)
        {
            var result = await _userRepository.GetAuditLogsPagedAsync(page, pageSize, userId, action, entityType, startDate, endDate);
            var dtos = System.Linq.Enumerable.Select(result.Items, l => new AuditLogDto
            {
                Id = l.Id,
                UserId = l.UserId,
                Action = l.Action,
                EntityType = l.EntityType,
                EntityId = l.EntityId,
                OldValue = l.OldValue,
                NewValue = l.NewValue,
                CreatedAt = l.CreatedAt
            });

            return new PagedResult<AuditLogDto>
            {
                Items = dtos,
                Page = page,
                PageSize = pageSize,
                TotalCount = result.TotalCount
            };
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
