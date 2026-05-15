using IamService.Application.Interfaces;
using IamService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IamService.Infrastructure.Persistence.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly IamDbContext _context;

        public RefreshTokenRepository(IamDbContext context) => _context = context;

        public async Task<RefreshToken?> FindByTokenAsync(string token)
            => await _context.Set<RefreshToken>()
                .FirstOrDefaultAsync(rt => rt.Token == token);

        public async Task AddAsync(RefreshToken refreshToken)
        {
            await _context.Set<RefreshToken>().AddAsync(refreshToken);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(RefreshToken refreshToken)
        {
            _context.Set<RefreshToken>().Update(refreshToken);
            await _context.SaveChangesAsync();
        }

        public async Task RevokeAllByUserIdAsync(Guid userId)
        {
            var tokens = await _context.Set<RefreshToken>()
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync();
            tokens.ForEach(t => t.IsRevoked = true);
            await _context.SaveChangesAsync();
        }
    }
}
