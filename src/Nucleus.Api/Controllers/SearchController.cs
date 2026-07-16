using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.Search.Commands;
using Nucleus.Application.Search.Queries;
using Nucleus.Api.Jobs;

namespace Nucleus.Api.Controllers;

/// <summary>
/// Search Hub endpoints — rankings, rank history, alerts, topic clusters,
/// content gaps, and page performance.
/// All endpoints are tenant-scoped via JWT claims.
/// </summary>
[ApiController]
[Route("api/search")]
[Authorize]
[Produces("application/json")]
public class SearchController(IMediator mediator, ICurrentTenantService tenant) : ControllerBase
{
    // ─── Rankings Dashboard ───────────────────────────────────────────────

    /// <summary>GET /api/search/rankings?brandId={id}</summary>
    [HttpGet("rankings")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetRankings([FromQuery] Guid brandId, CancellationToken ct)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var result = await mediator.Send(new GetRankingsDashboardQuery(brandId), ct);

        if (result is null)
            return NotFound(new { success = false, error = "Brand not found." });

        return Ok(new { success = true, data = result });
    }

    // ─── Rank History ─────────────────────────────────────────────────────

    /// <summary>GET /api/search/history/{keywordId}?days=90</summary>
    [HttpGet("history/{keywordId:guid}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetHistory(
        Guid keywordId,
        [FromQuery] int days = 90,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetRankHistoryQuery(keywordId, days), ct);

        if (result is null)
            return NotFound(new { success = false, error = "Keyword not found." });

        return Ok(new { success = true, data = result });
    }

    // ─── Rank Check (on-demand) ───────────────────────────────────────────

    /// <summary>POST /api/search/rankings/check — triggers DataForSEO rank check for a brand</summary>
    [HttpPost("rankings/check")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> TriggerRankCheck(
        [FromBody] TriggerRankCheckRequest req,
        CancellationToken ct)
    {
        var result = await mediator.Send(new TriggerRankCheckCommand(req.BrandId), ct);

        if (!result.Queued)
            return BadRequest(new { success = false, error = result.Message });

        // Enqueue the actual DataForSEO job (Hangfire — outside Application layer)
        BackgroundJob.Enqueue<KeywordRankJob>(job =>
            job.CheckRanksAsync(tenant.TenantId, req.BrandId, CancellationToken.None));

        return Ok(new { success = true, message = result.Message });
    }

    // ─── Alerts ───────────────────────────────────────────────────────────

    /// <summary>GET /api/search/alerts?brandId={id}&activeOnly=true</summary>
    [HttpGet("alerts")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] Guid brandId,
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var alerts = await mediator.Send(new GetSearchAlertsQuery(brandId, activeOnly), ct);
        return Ok(new { success = true, data = alerts });
    }

    /// <summary>POST /api/search/alerts — creates a new alert rule</summary>
    [HttpPost("alerts")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateAlert(
        [FromBody] CreateAlertRequest req,
        CancellationToken ct)
    {
        var id = await mediator.Send(
            new CreateSearchAlertCommand(req.BrandId, req.KeywordId, req.AlertType, req.Threshold), ct);

        return CreatedAtAction(nameof(GetAlerts),
            new { brandId = req.BrandId },
            new { success = true, data = new { id } });
    }

    /// <summary>DELETE /api/search/alerts/{id} — dismisses (deactivates) an alert</summary>
    [HttpDelete("alerts/{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DismissAlert(Guid id, CancellationToken ct)
    {
        var dismissed = await mediator.Send(new DismissAlertCommand(id), ct);
        return dismissed ? NoContent() : NotFound();
    }

    // ─── Topic Clusters ───────────────────────────────────────────────────

    /// <summary>GET /api/search/clusters?brandId={id}</summary>
    [HttpGet("clusters")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetClusters(
        [FromQuery] Guid brandId,
        CancellationToken ct)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var clusters = await mediator.Send(new GetTopicClustersQuery(brandId), ct);
        return Ok(new { success = true, data = clusters });
    }

    /// <summary>POST /api/search/clusters — creates a topic cluster</summary>
    [HttpPost("clusters")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateCluster(
        [FromBody] CreateClusterRequest req,
        CancellationToken ct)
    {
        var id = await mediator.Send(
            new CreateTopicClusterCommand(
                req.BrandId, req.Name, req.PillarKeyword,
                req.ClusterKeywords, req.Notes), ct);

        return CreatedAtAction(nameof(GetClusters),
            new { brandId = req.BrandId },
            new { success = true, data = new { id } });
    }

    // ─── Content Gaps ─────────────────────────────────────────────────────

    /// <summary>GET /api/search/gaps?brandId={id}</summary>
    [HttpGet("gaps")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetContentGaps(
        [FromQuery] Guid brandId,
        CancellationToken ct)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var gaps = await mediator.Send(new GetContentGapsQuery(brandId), ct);
        return Ok(new { success = true, data = gaps });
    }

    // ─── Page Performance ─────────────────────────────────────────────────

    /// <summary>GET /api/search/performance?brandId={id}</summary>
    [HttpGet("performance")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetPerformance(
        [FromQuery] Guid brandId,
        CancellationToken ct)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var performance = await mediator.Send(new GetPagePerformanceQuery(brandId), ct);
        return Ok(new { success = true, data = performance });
    }
}

// ─── Request models ───────────────────────────────────────────────────────────

public record TriggerRankCheckRequest(Guid BrandId);

public record CreateAlertRequest(
    Guid BrandId,
    Guid KeywordId,
    string AlertType,
    int Threshold);

public record CreateClusterRequest(
    Guid BrandId,
    string Name,
    string PillarKeyword,
    List<string> ClusterKeywords,
    string? Notes = null);
