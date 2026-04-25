using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Infrastructure.Auth;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;
    // In production, refresh tokens should be stored in the DB (not in-memory)
    private static readonly ConcurrentDictionary<string, (Guid UserId, DateTimeOffset Expiry)> _refreshTokens = new();

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public TokenPair GenerateTokenPair(ApplicationUser user)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key not configured");
        var issuer = _config["Jwt:Issuer"] ?? "nucleus";
        var audience = _config["Jwt:Audience"] ?? "nucleus";
        var expiresInMinutes = int.Parse(_config["Jwt:ExpiresInMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new Claim("role", user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateRefreshToken();

        _refreshTokens[refreshToken] = (user.Id, DateTimeOffset.UtcNow.AddDays(30));

        return new TokenPair(accessToken, refreshToken, expiresInMinutes * 60);
    }

    public (bool IsValid, Guid UserId) ValidateRefreshToken(string refreshToken)
    {
        if (_refreshTokens.TryGetValue(refreshToken, out var entry))
        {
            if (entry.Expiry > DateTimeOffset.UtcNow)
                return (true, entry.UserId);
            _refreshTokens.TryRemove(refreshToken, out _);
        }
        return (false, Guid.Empty);
    }

    public void RevokeRefreshToken(string refreshToken)
        => _refreshTokens.TryRemove(refreshToken, out _);

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
