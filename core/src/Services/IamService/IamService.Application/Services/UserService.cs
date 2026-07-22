using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Contracts.Messages;
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
        private readonly IMessagePublisher _messagePublisher;

        public UserService(
            IUserRepository userRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IMessagePublisher messagePublisher)
        {
            _userRepository = userRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _messagePublisher = messagePublisher;
        }

        /// <summary>
        /// Publishes a marker-code/name replication event via the bus outbox. Called BEFORE the
        /// repository save so the event row commits in the same transaction as the user change (N7).
        /// </summary>
        private Task PublishLecturerProfileChangedAsync(User user) =>
            _messagePublisher.PublishAsync(new LecturerProfileChanged(
                Guid.NewGuid(), user.Id, user.MarkerCode, user.FullName, null, DateTime.UtcNow));

        /// <summary>Persists an audit-trail row AND mirrors it onto the bus as an <see cref="AuditLogRecorded"/>
        /// so Reporting can serve the global audit-log viewer from its own DB. Publish-before-save: the
        /// outbox row is flushed inside AddAuditLogAsync's SaveChanges (N7).</summary>
        private async Task RecordAuditAsync(AuditLog log)
        {
            await _messagePublisher.PublishAsync(new AuditLogRecorded(
                Guid.NewGuid(), "iam", log.UserId, log.Action, log.EntityType,
                log.EntityId, log.OldValue, log.NewValue, null, DateTime.UtcNow));
            await _userRepository.AddAuditLogAsync(log);
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

            // Publish-before-save: the outbox row is flushed inside AddAsync's SaveChanges (N7).
            if (user.Role == UserRole.Lecturer)
            {
                await PublishLecturerProfileChangedAsync(user);
            }

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
            await RecordAuditAsync(auditLog);

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

            var previousMarkerCode = user.MarkerCode;
            var previousFullName = user.FullName;

            if (request.FullName != null) user.FullName = request.FullName;
            if (request.PhoneNumber != null) user.PhoneNumber = request.PhoneNumber;
            if (request.MarkerCode != null) user.MarkerCode = request.MarkerCode;
            if (request.Role.HasValue) user.Role = request.Role.Value;

            var profileReplicationChanged = user.Role == UserRole.Lecturer
                && (user.MarkerCode != previousMarkerCode || user.FullName != previousFullName);
            if (profileReplicationChanged)
            {
                // Publish-before-save: outbox row committed with the user update below (N7).
                await PublishLecturerProfileChangedAsync(user);
            }
            
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
            await RecordAuditAsync(auditLog);

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
                await RecordAuditAsync(auditLog);
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
                await RecordAuditAsync(auditLog);
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
