using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common;
using Nucleus.Infrastructure.Data;
using System.Security.Claims;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("api/v1/tenant")]
[Authorize]
[Produces("application/json")]
public class TenantController(NucleusDbContext db) : ControllerBase
{
    private Guid CurrentTenantId =>
        Guid.Parse(User.FindFirstValue("tenant_id") ?? Guid.Empty.ToString());

    private string CurrentRole =>
        User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    // GET /api/v1/tenant
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var tenant = await db.Tenants
            .Include(t => t.Brands)
            .Include(t => t.Users)
            .FirstOrDefaultAsync(t => t.Id == CurrentTenantId, ct);

        if (tenant is null) return NotFound(ApiResponse.Fail("Tenant not found."));

        return Ok(ApiResponse.Ok(new
        {
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Plan,
            tenant.IsActive,
            tenant.CreatedAt,
            BrandCount = tenant.Brands.Count,
            UserCount = tenant.Users.Count,
        }));
    }

    // PUT /api/v1/tenant
    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateTenantRequest req, CancellationToken ct)
    {
        if (CurrentRole != "TenantAdmin")
            return Forbid();

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == CurrentTenantId, ct);
        if (tenant is null) return NotFound(ApiResponse.Fail("Tenant not found."));

        if (!string.IsNullOrWhiteSpace(req.Name))
        {
            tenant.Name = req.Name.Trim();
            tenant.Slug = req.Name.Trim().ToLowerInvariant()
                .Replace(" ", "-")
                .Replace("_", "-");
        }

        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse.Ok(new { tenant.Id, tenant.Name, tenant.Slug }));
    }
}

public record UpdateTenantRequest(string? Name);
