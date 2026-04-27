using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common;
using Nucleus.Domain.Entities;
using Nucleus.Infrastructure.Data;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "SuperAdmin")]
[Produces("application/json")]
public class AdminController(
    NucleusDbContext db,
    UserManager<ApplicationUser> userManager,
    ILogger<AdminController> logger) : ControllerBase
{
    // GET /api/v1/admin/audit?tenantId=&entityType=&page=1&pageSize=50
    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] Guid? tenantId,
        [FromQuery] string? entityType,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.AuditLogs.IgnoreQueryFilters().AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(a => a.TenantId == tenantId.Value);
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrEmpty(action))
            query = query.Where(a => a.Action == action);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id, a.TenantId, a.UserId, a.Action,
                a.EntityType, a.EntityId, a.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(ApiResponse.Ok(new { total, page, pageSize, items }));
    }

    // POST /api/v1/admin/users/{userId}/roles — assign role to a user
    [HttpPost("users/{userId:guid}/roles")]
    public async Task<IActionResult> AssignRole(Guid userId, [FromBody] RoleRequest req)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound(ApiResponse.Fail("User not found."));

        if (!await userManager.IsInRoleAsync(user, req.Role))
            await userManager.AddToRoleAsync(user, req.Role);

        logger.LogInformation("Assigned role {Role} to user {UserId}", req.Role, userId);
        return Ok(ApiResponse.Ok(new { userId, req.Role }));
    }

    // DELETE /api/v1/admin/users/{userId}/roles — remove role from a user
    [HttpDelete("users/{userId:guid}/roles")]
    public async Task<IActionResult> RemoveRole(Guid userId, [FromBody] RoleRequest req)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound(ApiResponse.Fail("User not found."));

        await userManager.RemoveFromRoleAsync(user, req.Role);

        logger.LogInformation("Removed role {Role} from user {UserId}", req.Role, userId);
        return Ok(ApiResponse.Ok(new { userId, req.Role }));
    }
}

public record RoleRequest(string Role);
