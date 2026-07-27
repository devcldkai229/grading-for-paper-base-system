using IamService.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace IamService.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> FindByEmailAsync(string email);
        Task<User?> FindByGoogleIdAsync(string googleId);
        Task<User?> FindByIdAsync(Guid id);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task DeleteAsync(User user);
        Task<bool> HasAssociatedDataAsync(Guid userId);
        Task<(System.Collections.Generic.IEnumerable<User> Items, int TotalCount)> GetUsersPagedAsync(int page, int pageSize, string? search);
        Task AddAuditLogAsync(AuditLog auditLog);
        Task<(System.Collections.Generic.IEnumerable<AuditLog> Items, int TotalCount)> GetAuditLogsPagedAsync(
            int page, 
            int pageSize, 
            Guid? userId, 
            string? action, 
            string? entityType, 
            DateTime? startDate, 
            DateTime? endDate);
        Task<User?> FindByResetTokenAsync(string token);
    }
}
