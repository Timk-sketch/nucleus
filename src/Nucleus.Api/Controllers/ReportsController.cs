using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nucleus.Application.ReportsHub.Queries;

namespace Nucleus.Api.Controllers;

/// <summary>
/// Reports Hub — cross-hub analytics aggregation.
///
///   GET /api/reports/overview?brandId=&days=30      — KPI snapshot
///   GET /api/reports/content?brandId=&days=30       — content + AI cost
///   GET /api/reports/search?brandId=                — SEO rankings
///   GET /api/reports/finders?brandId=&days=30       — finder conversions
///   GET /api/reports/distribution?brandId=&days=30  — email + social
/// </summary>
[ApiController]
[Produces("application/json")]
[Authorize]
public class ReportsController(IMediator mediator) : ControllerBase
{
    /// <summary>GET /api/reports/overview?brandId=&days= — cross-hub KPI snapshot</summary>
    [HttpGet("api/reports/overview")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetOverview(
        [FromQuery] Guid brandId,
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var data = await mediator.Send(new GetBrandOverviewQuery(brandId, days), ct);
        return data is null
            ? NotFound(new { success = false, error = "Brand not found." })
            : Ok(new { success = true, data });
    }

    /// <summary>GET /api/reports/content?brandId=&days= — content performance + AI cost</summary>
    [HttpGet("api/reports/content")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetContentReport(
        [FromQuery] Guid brandId,
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var data = await mediator.Send(new GetContentReportQuery(brandId, days), ct);
        return data is null
            ? NotFound(new { success = false, error = "Brand not found." })
            : Ok(new { success = true, data });
    }

    /// <summary>GET /api/reports/search?brandId= — SEO keyword rankings</summary>
    [HttpGet("api/reports/search")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSearchReport(
        [FromQuery] Guid brandId,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var data = await mediator.Send(new GetSearchReportQuery(brandId), ct);
        return data is null
            ? NotFound(new { success = false, error = "Brand not found." })
            : Ok(new { success = true, data });
    }

    /// <summary>GET /api/reports/finders?brandId=&days= — finder conversion funnel</summary>
    [HttpGet("api/reports/finders")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetFinderReport(
        [FromQuery] Guid brandId,
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var data = await mediator.Send(new GetFinderReportQuery(brandId, days), ct);
        return data is null
            ? NotFound(new { success = false, error = "Brand not found." })
            : Ok(new { success = true, data });
    }

    /// <summary>GET /api/reports/distribution?brandId=&days= — email + social analytics</summary>
    [HttpGet("api/reports/distribution")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetDistributionReport(
        [FromQuery] Guid brandId,
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var data = await mediator.Send(new GetDistributionReportQuery(brandId, days), ct);
        return data is null
            ? NotFound(new { success = false, error = "Brand not found." })
            : Ok(new { success = true, data });
    }
}
