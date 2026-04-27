using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common;
using Nucleus.Domain.Entities;
using Nucleus.Infrastructure.Data;
using System.Security.Claims;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
[Produces("application/json")]
public class UsersController(
    UserManager<ApplicationUser> userManager,
    NucleusDbContext db) : ControllerBase
{
    private Guid CurrentTenantId =>
        Guid.Parse(User.FindFirstValue("tenant_id") ?? Guid.Empty.ToString());

    private string CurrentRole =>
        User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    // GET /api/v1/users
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var users = await db.Users
            .Where(u => u.TenantId == CurrentTenantId)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Role,
                u.CreatedAt,
            })
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(ct);

        return Ok(ApiResponse.Ok(users));
    }

    // POST /api/v1/users/invite
    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteUserRequest req)
    {
        if (CurrentRole != "TenantAdmin")
            return Forbid();

        if (await userManager.FindByEmailAsync(req.Email) != null)
            return Conflict(ApiResponse.Fail("A user with that email already exists."));

        var role = req.Role is "TenantAdmin" or "BrandEditor" ? req.Role : "BrandEditor";

        var user = new ApplicationUser
        {
            UserName = req.Email,
            Email = req.Email,
            FirstName = req.FirstName,
            LastName = req.LastName,
            TenantId = CurrentTenantId,
            Role = role,
        };

        // Generate a temporary password — user must change on first login
        var tempPassword = GenerateTempPassword();
        var result = await userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
            return BadRequest(ApiResponse.Fail(string.Join("; ", result.Errors.Select(e => e.Description))));

        await userManager.AddToRoleAsync(user, role);

        // TODO Sprint 7: send invite email via MailKit with tempPassword
        return Ok(ApiResponse.Ok(new
        {
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Role,
            TempPassword = tempPassword, // returned once — will be emailed in Sprint 7
        }));
    }

    // PUT /api/v1/users/{id}/role
    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest req)
    {
        if (CurrentRole != "TenantAdmin")
            return Forbid();

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null || user.TenantId != CurrentTenantId)
            return NotFound(ApiResponse.Fail("User not found."));

        // Prevent self-demotion
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub") ?? Guid.Empty.ToString());
        if (user.Id == callerId)
            return BadRequest(ApiResponse.Fail("You cannot change your own role."));

        var validRole = req.Role is "TenantAdmin" or "BrandEditor" ? req.Role : "BrandEditor";

        var currentRoles = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, currentRoles);
        await userManager.AddToRoleAsync(user, validRole);

        user.Role = validRole;
        await userManager.UpdateAsync(user);

        return Ok(ApiResponse.Ok(new { user.Id, user.Role }));
    }

    // DELETE /api/v1/users/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id)
    {
        if (CurrentRole != "TenantAdmin")
            return Forbid();

        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub") ?? Guid.Empty.ToString());
        if (id == callerId)
            return BadRequest(ApiResponse.Fail("You cannot remove yourself."));

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null || user.TenantId != CurrentTenantId)
            return NotFound(ApiResponse.Fail("User not found."));

        // Revoke all refresh tokens before deleting
        var tokens = db.RefreshTokens.Where(t => t.UserId == user.Id && !t.IsRevoked);
        await foreach (var t in tokens.AsAsyncEnumerable())
            t.IsRevoked = true;
        await db.SaveChangesAsync();

        await userManager.DeleteAsync(user);

        return NoContent();
    }

    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$";
        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[16];
        rng.GetBytes(bytes);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
}

public record InviteUserRequest(string Email, string FirstName, string LastName, string Role);
public record UpdateRoleRequest(string Role);
