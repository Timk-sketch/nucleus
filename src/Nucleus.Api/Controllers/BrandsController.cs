using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Nucleus.Application.Brands.Commands;
using Nucleus.Application.Common;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Api.Jobs;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("api/v1/brands")]
[Authorize]
[Produces("application/json")]
public class BrandsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenantService;
    private readonly IMemoryCache _cache;
    private readonly IAuditService _audit;

    public BrandsController(IMediator mediator, INucleusDbContext db,
        ICurrentTenantService tenantService, IMemoryCache cache, IAuditService audit)
    {
        _mediator = mediator;
        _db = db;
        _tenantService = tenantService;
        _cache = cache;
        _audit = audit;
    }

    private string BrandListCacheKey() => $"brands:{_tenantService.TenantId}";
    private Guid? CurrentUserId() => Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id) ? id : null;

    [HttpPost]
    [ProducesResponseType(typeof(CreateBrandResult), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> Create([FromBody] CreateBrandRequest req, CancellationToken ct)
    {
        // Plan gate: starter tenants are limited to 1 brand
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _tenantService.TenantId, ct);
        if (tenant?.Plan == "starter")
        {
            var brandCount = await _db.Brands.CountAsync(ct);
            if (brandCount >= 1)
                return StatusCode(403, ApiResponse.Fail(
                    "Starter plan is limited to 1 brand. Upgrade to Pro for unlimited brands."));
        }

        var result = await _mediator.Send(new CreateBrandCommand(
            req.Code, req.Name, req.Domain, req.PrimaryColor,
            req.WpSiteUrl, req.WpUsername, req.WpAppPassword,
            req.GhlLocationId, req.GhlApiKey, req.BrandVoice), ct);

        BackgroundJob.Enqueue<BrandProvisioningJob>(job => job.RunAsync(result.BrandId));
        _cache.Remove(BrandListCacheKey());
        await _audit.LogAsync(_tenantService.TenantId, CurrentUserId(), "created", "Brand", result.BrandId.ToString(), ct: ct);

        return CreatedAtAction(nameof(GetById), new { id = result.BrandId }, result);
    }

    [HttpGet]
    [ProducesResponseType(200)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var cacheKey = BrandListCacheKey();
        if (!_cache.TryGetValue(cacheKey, out object? cached))
        {
            cached = await _db.Brands
                .Select(b => new
                {
                    b.Id, b.Code, b.Name, b.Domain, b.Slug,
                    b.PrimaryColor, b.Status, b.OnboardingStep,
                    b.OnboardingCompletedAt, b.ServicesProvisioned
                })
                .ToListAsync(ct);
            _cache.Set(cacheKey, cached, TimeSpan.FromSeconds(60));
        }

        return Ok(new { success = true, data = cached });
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var brand = await _db.Brands
            .Include(b => b.ProvisioningSteps)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (brand is null) return NotFound();

        return Ok(new { success = true, data = brand });
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBrandRequest req, CancellationToken ct)
    {
        var brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (brand is null) return NotFound();

        brand.Name = req.Name ?? brand.Name;
        brand.Domain = req.Domain ?? brand.Domain;
        brand.PrimaryColor = req.PrimaryColor ?? brand.PrimaryColor;
        brand.WpSiteUrl = req.WpSiteUrl ?? brand.WpSiteUrl;
        brand.WpUsername = req.WpUsername ?? brand.WpUsername;
        if (req.WpAppPassword != null) brand.WpAppPassword = req.WpAppPassword;
        brand.GhlLocationId = req.GhlLocationId ?? brand.GhlLocationId;
        if (req.GhlApiKey != null) brand.GhlApiKey = req.GhlApiKey;
        brand.BrandVoice = req.BrandVoice ?? brand.BrandVoice;

        await _db.SaveChangesAsync(ct);
        _cache.Remove(BrandListCacheKey());
        await _audit.LogAsync(_tenantService.TenantId, CurrentUserId(), "updated", "Brand", id.ToString(), ct: ct);

        return Ok(new { success = true, data = new { brand.Id, brand.Name, brand.Status } });
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var brand = await _db.Brands.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (brand is null) return NotFound();

        _db.Brands.Remove(brand);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(BrandListCacheKey());
        await _audit.LogAsync(_tenantService.TenantId, CurrentUserId(), "deleted", "Brand", id.ToString(), ct: ct);

        return NoContent();
    }

    [HttpGet("{id:guid}/provisioning-status")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ProvisioningStatus(Guid id, CancellationToken ct)
    {
        var steps = await _db.BrandProvisioningSteps
            .Where(s => s.BrandId == id)
            .Select(s => new
            {
                s.StepName, s.Status, s.ErrorMessage,
                s.CompletedAt, s.AttemptCount
            })
            .ToListAsync(ct);

        if (!steps.Any()) return NotFound();

        return Ok(new { success = true, data = steps });
    }
}

public record CreateBrandRequest(
    string Code,
    string Name,
    string Domain,
    string PrimaryColor,
    string? WpSiteUrl,
    string? WpUsername,
    string? WpAppPassword,
    string? GhlLocationId,
    string? GhlApiKey,
    string? BrandVoice);

public record UpdateBrandRequest(
    string? Name,
    string? Domain,
    string? PrimaryColor,
    string? WpSiteUrl,
    string? WpUsername,
    string? WpAppPassword,
    string? GhlLocationId,
    string? GhlApiKey,
    string? BrandVoice);
