using IamService.Application.Interfaces;
using IamService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace IamService.Infrastructure.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IamDbContext _context;

        public UserRepository(IamDbContext context) => _context = context;

        public async Task<User?> FindByEmailAsync(string email)
            => await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        public async Task<User?> FindByGoogleIdAsync(string googleId)
            => await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId);

        public async Task<User?> FindByIdAsync(Guid id)
            => await _context.Users.FindAsync(id);

        public async Task AddAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }
    }
}
