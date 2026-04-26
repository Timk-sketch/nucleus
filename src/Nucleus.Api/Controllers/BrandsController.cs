using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Brands.Commands;
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

    public BrandsController(IMediator mediator, INucleusDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateBrandResult), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> Create([FromBody] CreateBrandRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateBrandCommand(
            req.Code, req.Name, req.Domain, req.PrimaryColor,
            req.WpSiteUrl, req.WpUsername, req.WpAppPassword,
            req.GhlLocationId, req.GhlApiKey, req.BrandVoice), ct);

        BackgroundJob.Enqueue<BrandProvisioningJob>(job => job.RunAsync(result.BrandId));

        return CreatedAtAction(nameof(GetById), new { id = result.BrandId }, result);
    }

    [HttpGet]
    [ProducesResponseType(200)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var brands = await _db.Brands
            .Select(b => new
            {
                b.Id, b.Code, b.Name, b.Domain, b.Slug,
                b.PrimaryColor, b.Status, b.OnboardingStep,
                b.OnboardingCompletedAt, b.ServicesProvisioned
            })
            .ToListAsync(ct);

        return Ok(new { success = true, data = brands });
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
