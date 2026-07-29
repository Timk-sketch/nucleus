using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nucleus.Application.LeadsHub.Queries;
using System.Text;

namespace Nucleus.Api.Controllers;

/// <summary>
/// Leads Hub — view and export leads captured via Finder sessions.
///
///   GET /api/leads?brandId=&finderId=&days=30&page=1&pageSize=50 — paginated leads
///   GET /api/leads/export?brandId=&finderId=&days=30             — CSV download
/// </summary>
[ApiController]
[Produces("application/json")]
[Authorize]
public class LeadsController(IMediator mediator) : ControllerBase
{
    /// <summary>GET /api/leads — paginated leads for a brand</summary>
    [HttpGet("api/leads")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetLeads(
        [FromQuery] Guid brandId,
        [FromQuery] Guid? finderId = null,
        [FromQuery] int days = 30,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var data = await mediator.Send(new GetBrandLeadsQuery(brandId, finderId, days, page, pageSize), ct);
        return data is null
            ? NotFound(new { success = false, error = "Brand not found." })
            : Ok(new { success = true, data });
    }

    /// <summary>GET /api/leads/export — CSV download</summary>
    [HttpGet("api/leads/export")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ExportLeads(
        [FromQuery] Guid brandId,
        [FromQuery] Guid? finderId = null,
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var result = await mediator.Send(new ExportLeadsCsvQuery(brandId, finderId, days), ct);
        if (result is null)
            return NotFound(new { success = false, error = "Brand not found." });

        var (fileName, csv) = result.Value;
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }
}
