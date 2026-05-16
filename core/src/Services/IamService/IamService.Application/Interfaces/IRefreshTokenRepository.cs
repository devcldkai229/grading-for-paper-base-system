using IamService.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace IamService.Application.Interfaces
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> FindByTokenAsync(string token);
        Task AddAsync(RefreshToken refreshToken);
        Task UpdateAsync(RefreshToken refreshToken);
        Task RevokeAllByUserIdAsync(Guid userId);
    }
}
