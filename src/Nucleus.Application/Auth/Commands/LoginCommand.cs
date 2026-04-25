using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.Auth.Commands;

public record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public record LoginResult(bool Succeeded, string? AccessToken, string? RefreshToken, int ExpiresIn, string? Error);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginCommandHandler(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwtTokenService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return new LoginResult(false, null, null, 0, "Invalid email or password.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            var error = result.IsLockedOut ? "Account is locked. Try again later." : "Invalid email or password.";
            return new LoginResult(false, null, null, 0, error);
        }

        var tokens = _jwtTokenService.GenerateTokenPair(user);
        return new LoginResult(true, tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn, null);
    }
}
