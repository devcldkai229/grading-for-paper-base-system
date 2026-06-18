using IamService.Application.Interfaces;
using IamService.Domain.Entities;
using IamService.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace IamService.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IamDbContext _context;

    public RefreshTokenRepository(IamDbContext context) => _context = context;

    public async Task<RefreshToken?> FindByTokenAsync(string token)
    {
        var tokenHash = TokenHashHelper.Hash(token);
        return await _context.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash);
    }

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
