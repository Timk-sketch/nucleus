using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.Auth.Commands;

public record RegisterCommand(
    string TenantName,
    string Email,
    string Password,
    string FirstName,
    string LastName) : IRequest<RegisterResult>;

public record RegisterResult(bool Succeeded, string? AccessToken, string? RefreshToken, int ExpiresIn, string[] Errors);

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.TenantName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(50);
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly INucleusDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;

    public RegisterCommandHandler(
        INucleusDbContext db,
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService)
    {
        _db = db;
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var tenant = new Tenant
        {
            Name = request.TenantName,
            Slug = GenerateSlug(request.TenantName),
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(cancellationToken);

        var user = new ApplicationUser
        {
            TenantId = tenant.Id,
            Email = request.Email,
            UserName = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = "TenantAdmin",
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return new RegisterResult(false, null, null, 0, result.Errors.Select(e => e.Description).ToArray());

        await _userManager.AddToRoleAsync(user, "TenantAdmin");

        var tokens = _jwtTokenService.GenerateTokenPair(user);
        return new RegisterResult(true, tokens.AccessToken, tokens.RefreshToken, tokens.ExpiresIn, Array.Empty<string>());
    }

    private static string GenerateSlug(string name)
        => name.ToLowerInvariant()
               .Replace(" ", "-")
               .Where(c => char.IsLetterOrDigit(c) || c == '-')
               .Aggregate("", (acc, c) => acc + c);
}
