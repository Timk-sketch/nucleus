using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Infrastructure.Auth;

public class JwtTokenService(IConfiguration config) : IJwtTokenService
{
    private readonly string _secret = config["JWT_SECRET"]
        ?? throw new InvalidOperationException("JWT_SECRET not configured");
    private readonly int _expiryMinutes = int.TryParse(config["JWT_EXPIRY_MINUTES"], out var m) ? m : 60;
    private readonly int _refreshDays = int.TryParse(config["JWT_REFRESH_EXPIRY_DAYS"], out var d) ? d : 30;

    public TokenPair GenerateTokenPair(ApplicationUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new("tenant_id", user.TenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, user.Role),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        return new TokenPair(accessToken, refreshToken, _expiryMinutes * 60);
    }

    public (bool IsValid, Guid UserId) ValidateRefreshToken(string refreshToken)
    {
        // DB-backed validation is done in AuthController
        return (false, Guid.Empty);
    }

    public void RevokeRefreshToken(string refreshToken)
    {
        // DB-backed revocation is handled in AuthController
    }
}
