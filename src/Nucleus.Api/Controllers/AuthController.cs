using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common;
using Nucleus.Domain.Entities;
using Nucleus.Infrastructure.Auth;
using Nucleus.Infrastructure.Data;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    JwtTokenService jwtService,
    NucleusDbContext db) : ControllerBase
{
    // POST /api/v1/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (await userManager.FindByEmailAsync(req.Email) != null)
            return Conflict(ApiResponse.Fail("Email already registered"));

        var tenant = new Tenant
        {
            Name = req.CompanyName,
            Slug = req.CompanyName.ToLowerInvariant().Replace(" ", "-"),
            Plan = "starter",
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            FirstName = req.FirstName,
            LastName = req.LastName,
            TenantId = tenant.Id,
            Role = "TenantAdmin",
        };

        var result = await userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(ApiResponse.Fail(string.Join("; ", result.Errors.Select(e => e.Description))));

        await userManager.AddToRoleAsync(user, "TenantAdmin");

        var tokenPair = jwtService.GenerateTokenPair(user);
        var refreshToken = new Nucleus.Domain.Entities.RefreshToken
        {
            UserId = user.Id, TenantId = user.TenantId,
            Token = tokenPair.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        };
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        return Ok(ApiResponse.Ok(new TokenResponse(tokenPair.AccessToken, tokenPair.RefreshToken, tokenPair.ExpiresIn)));
    }

    // POST /api/v1/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user == null) return Unauthorized(ApiResponse.Fail("Invalid credentials"));

        var result = await signInManager.CheckPasswordSignInAsync(user, req.Password, false);
        if (!result.Succeeded) return Unauthorized(ApiResponse.Fail("Invalid credentials"));

        var tokenPair = jwtService.GenerateTokenPair(user);
        var refreshToken = new Nucleus.Domain.Entities.RefreshToken
        {
            UserId = user.Id, TenantId = user.TenantId,
            Token = tokenPair.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        };
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync();

        return Ok(ApiResponse.Ok(new TokenResponse(tokenPair.AccessToken, tokenPair.RefreshToken, tokenPair.ExpiresIn)));
    }

    // POST /api/v1/auth/refresh
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == req.RefreshToken && !r.IsRevoked);

        if (stored == null || stored.ExpiresAt < DateTimeOffset.UtcNow)
            return Unauthorized(ApiResponse.Fail("Invalid or expired refresh token"));

        stored.IsRevoked = true;

        var user = await userManager.FindByIdAsync(stored.UserId.ToString());
        if (user == null) return Unauthorized(ApiResponse.Fail("User not found"));

        var tokenPair = jwtService.GenerateTokenPair(user);
        var newRefresh = new Nucleus.Domain.Entities.RefreshToken
        {
            UserId = user.Id, TenantId = user.TenantId,
            Token = tokenPair.RefreshToken,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        };
        db.RefreshTokens.Add(newRefresh);
        await db.SaveChangesAsync();

        return Ok(ApiResponse.Ok(new TokenResponse(tokenPair.AccessToken, tokenPair.RefreshToken, tokenPair.ExpiresIn)));
    }
}

public record RegisterRequest(string Email, string Password, string FirstName, string LastName, string CompanyName);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
