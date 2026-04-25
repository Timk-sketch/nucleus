using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResult>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResult>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    public RefreshTokenCommandHandler(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<LoginResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var (isValid, userId) = _jwtTokenService.ValidateRefreshToken(request.RefreshToken);
        if (!isValid)
            return new LoginResult(false, null, null, 0, "Invalid or expired refresh token.");

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return new LoginResult(false, null, null, 0, "User not found.");

        _jwtTokenService.RevokeRefreshToken(request.RefreshToken);
        var tokens = _jwtTokenService.GenerateTokenPair(user);
        return new LoginResult(true, tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn, null);
    }
}
