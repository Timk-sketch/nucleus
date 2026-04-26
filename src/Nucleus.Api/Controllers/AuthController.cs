using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common;
using Nucleus.Domain.Entities;
using Nucleus.Infrastructure.Auth;
using Nucleus.Infrastructure.Data;
using System.Security.Claims;

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
    [EnableRateLimiting("auth")]
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
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await userManager.FindByEmailAsync(req.Email);
        if (user == null) return Unauthorized(ApiResponse.Fail("Invalid credentials"));

        var result = await signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            var error = result.IsLockedOut
                ? "Account locked due to too many failed attempts. Try again in 15 minutes."
                : "Invalid credentials";
            return Unauthorized(ApiResponse.Fail(error));
        }

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
    [EnableRateLimiting("auth")]
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

    // POST /api/v1/auth/change-password
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (userId == null) return Unauthorized(ApiResponse.Fail("Not authenticated"));

        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized(ApiResponse.Fail("User not found"));

        var result = await userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
            return BadRequest(ApiResponse.Fail(string.Join("; ", result.Errors.Select(e => e.Description))));

        // Revoke all existing refresh tokens — force re-login on other devices
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.IsRevoked)
            .ToListAsync();
        foreach (var t in tokens) t.IsRevoked = true;
        await db.SaveChangesAsync();

        return Ok(ApiResponse.Ok(new { message = "Password changed successfully" }));
    }

    // GET /api/v1/auth/me
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (userId == null) return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        return Ok(ApiResponse.Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role,
            user.TenantId,
        }));
    }

    // PUT /api/v1/auth/profile
    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (userId == null) return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        user.FirstName = req.FirstName?.Trim() ?? user.FirstName;
        user.LastName = req.LastName?.Trim() ?? user.LastName;
        await userManager.UpdateAsync(user);

        return Ok(ApiResponse.Ok(new { user.FirstName, user.LastName }));
    }
}

public record RegisterRequest(string Email, string Password, string FirstName, string LastName, string CompanyName);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record UpdateProfileRequest(string? FirstName, string? LastName);
public record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);
