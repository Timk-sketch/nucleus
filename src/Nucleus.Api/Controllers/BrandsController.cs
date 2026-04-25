using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Brands.Commands;
using Nucleus.Application.Common.Interfaces;

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
    [Authorize(Roles = "TenantAdmin,SuperAdmin")]
    [ProducesResponseType(typeof(CreateBrandResult), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> Create([FromBody] CreateBrandRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateBrandCommand(
            req.Code, req.Name, req.Domain, req.PrimaryColor,
            req.WpSiteUrl, req.WpUsername, req.WpAppPassword,
            req.GhlLocationId, req.GhlApiKey, req.BrandVoice), ct);

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
                b.OnboardingCompletedAt, b.ServicesProvisionedJson
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
