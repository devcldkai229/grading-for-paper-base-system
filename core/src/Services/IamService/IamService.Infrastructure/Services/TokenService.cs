using IamService.Application.Features.Auth;
using IamService.Application.Interfaces;
using IamService.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace IamService.Infrastructure.Services
{
    public class JwtSettings
    {
        public string Secret { get; set; } = string.Empty;
        public int AccessTokenExpirationMinutes { get; set; } = 15;
        public int RefreshTokenExpirationDays { get; set; } = 7;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
    }

    public class TokenService : ITokenService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly IRefreshTokenRepository _refreshTokenRepo;
        private readonly IUserRepository _userRepo;
        private readonly TokenValidationParameters _tokenValidationParams;

        public TokenService(
            IOptions<JwtSettings> jwtSettings,
            IRefreshTokenRepository refreshTokenRepo,
            IUserRepository userRepo,
            TokenValidationParameters tokenValidationParams)
        {
            _jwtSettings = jwtSettings.Value;
            _refreshTokenRepo = refreshTokenRepo;
            _userRepo = userRepo;
            _tokenValidationParams = tokenValidationParams;
        }

        public async Task<AuthResult> GenerateTokensAsync(User user)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSettings.Secret);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("Role", user.Role.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience
            };

            var token = jwtTokenHandler.CreateToken(tokenDescriptor);
            var jwtToken = jwtTokenHandler.WriteToken(token);

            var refreshToken = new RefreshToken
            {
                JwtId = token.Id,
                IsUsed = false,
                IsRevoked = false,
                UserId = user.Id,
                AddedDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
                Token = RandomString(35) + Guid.NewGuid()
            };

            await _refreshTokenRepo.AddAsync(refreshToken);

            return new AuthResult
            {
                Success = true,
                AccessToken = jwtToken,
                RefreshToken = refreshToken.Token,
                ExpiresAt = tokenDescriptor.Expires
            };
        }

        public async Task<AuthResult> VerifyAndGenerateTokenAsync(string token, string refreshToken)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();

            try
            {
                // Validate the token (without checking expiry)
                var validationParams = _tokenValidationParams.Clone();
                validationParams.ValidateLifetime = false;

                var principal = jwtTokenHandler.ValidateToken(token, validationParams, out var validatedToken);

                if (validatedToken is JwtSecurityToken jwtSecurityToken)
                {
                    var result = jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase);

                    if (result == false)
                    {
                        return new AuthResult { Success = false, Errors = ["Invalid token algorithm"] };
                    }

                    if (jwtSecurityToken.ValidTo > DateTime.UtcNow)
                    {
                        return new AuthResult { Success = false, Errors = ["Token hasn't expired yet"] };
                    }

                    var storedToken = await _refreshTokenRepo.FindByTokenAsync(refreshToken);

                    if (storedToken == null)
                    {
                        return new AuthResult { Success = false, Errors = ["Refresh token does not exist"] };
                    }

                    if (DateTime.UtcNow > storedToken.ExpiryDate)
                    {
                        return new AuthResult { Success = false, Errors = ["Refresh token has expired"] };
                    }

                    if (storedToken.IsUsed)
                    {
                        return new AuthResult { Success = false, Errors = ["Refresh token has been used"] };
                    }

                    if (storedToken.IsRevoked)
                    {
                        return new AuthResult { Success = false, Errors = ["Refresh token has been revoked"] };
                    }

                    var jti = jwtSecurityToken.Claims.SingleOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;

                    if (storedToken.JwtId != jti)
                    {
                        return new AuthResult { Success = false, Errors = ["Refresh token does not match this JWT"] };
                    }

                    // Update current token
                    storedToken.IsUsed = true;
                    await _refreshTokenRepo.UpdateAsync(storedToken);

                    // Generate new tokens
                    var userId = jwtSecurityToken.Claims.SingleOrDefault(x => x.Type == JwtRegisteredClaimNames.Sub)?.Value;
                    if (string.IsNullOrEmpty(userId))
                    {
                        return new AuthResult { Success = false, Errors = ["Invalid token claims"] };
                    }

                    var user = await _userRepo.FindByIdAsync(Guid.Parse(userId));

                    if (user == null)
                    {
                        return new AuthResult { Success = false, Errors = ["User not found"] };
                    }

                    return await GenerateTokensAsync(user);
                }
                
                return new AuthResult { Success = false, Errors = ["Invalid token format"] };
            }
            catch (Exception ex)
            {
                return new AuthResult { Success = false, Errors = [$"Error validating token: {ex.Message}"] };
            }
        }

        private DateTime UnixTimeStampToDateTime(long unixTimeStamp)
        {
            var dateTimeVal = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTimeVal = dateTimeVal.AddSeconds(unixTimeStamp);
            return dateTimeVal;
        }

        private string RandomString(int length)
        {
            var random = new Random();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
