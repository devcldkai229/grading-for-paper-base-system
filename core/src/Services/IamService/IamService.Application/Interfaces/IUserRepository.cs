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
    }
}
