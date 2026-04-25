using Nucleus.Domain.Entities;

namespace Nucleus.Application.Common.Interfaces;

public record TokenPair(string AccessToken, string RefreshToken, int ExpiresIn);

public interface IJwtTokenService
{
    TokenPair GenerateTokenPair(ApplicationUser user);
    (bool IsValid, Guid UserId) ValidateRefreshToken(string refreshToken);
    void RevokeRefreshToken(string refreshToken);
}
