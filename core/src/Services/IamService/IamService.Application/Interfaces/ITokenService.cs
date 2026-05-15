using IamService.Domain.Entities;
using IamService.Application.Features.Auth;
using System.Threading.Tasks;

namespace IamService.Application.Interfaces
{
    public interface ITokenService
    {
        Task<AuthResult> GenerateTokensAsync(User user);
        Task<AuthResult> VerifyAndGenerateTokenAsync(string token, string refreshToken);
    }
}
