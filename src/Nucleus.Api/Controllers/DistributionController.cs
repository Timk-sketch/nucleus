using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nucleus.Application.Distribution.Commands;
using Nucleus.Application.Distribution.Queries;

namespace Nucleus.Api.Controllers;

/// <summary>
/// Distribution Hub endpoints — social scheduling, email campaigns, send log.
/// All endpoints are tenant-scoped via JWT claims (ICurrentTenantService injected in handlers).
///
/// Social:   POST   /api/distribution/social          — schedule a post
///           GET    /api/distribution/social?brandId  — get social schedule
/// Email:    POST   /api/distribution/email           — create a campaign
///           POST   /api/distribution/email/send      — send a campaign
///           GET    /api/distribution/email?brandId   — list campaigns
///           GET    /api/distribution/email/{id}/stats — campaign stats
/// SendLog:  GET    /api/distribution/sendlog?brandId — send log
/// </summary>
[ApiController]
[Route("api/distribution")]
[Authorize]
[Produces("application/json")]
public class DistributionController(IMediator mediator) : ControllerBase
{
    // ─── Social Schedule ──────────────────────────────────────────────────

    /// <summary>GET /api/distribution/social?brandId={id}&from={dt}&to={dt}&status={s}</summary>
    [HttpGet("social")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetSocialSchedule(
        [FromQuery] Guid brandId,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var posts = await mediator.Send(
            new GetSocialScheduleQuery(brandId, from, to, status), ct);

        return Ok(new { success = true, data = posts });
    }

    /// <summary>POST /api/distribution/social — schedule a social post</summary>
    [HttpPost("social")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SchedulePost(
        [FromBody] SchedulePostRequest req,
        CancellationToken ct)
    {
        try
        {
            var id = await mediator.Send(
                new ScheduleSocialPostCommand(
                    req.BrandId,
                    req.Platform,
                    req.Caption,
                    req.ScheduledAt,
                    req.ImageUrl,
                    req.ContentPageId,
                    req.Provider ?? "ghl"), ct);

            return CreatedAtAction(nameof(GetSocialSchedule),
                new { brandId = req.BrandId },
                new { success = true, data = new { id } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ─── Email Campaigns ──────────────────────────────────────────────────

    /// <summary>GET /api/distribution/email?brandId={id}</summary>
    [HttpGet("email")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetCampaigns(
        [FromQuery] Guid brandId,
        CancellationToken ct)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var campaigns = await mediator.Send(new GetEmailCampaignsQuery(brandId), ct);
        return Ok(new { success = true, data = campaigns });
    }

    /// <summary>GET /api/distribution/email/{id}/stats?brandId={id}</summary>
    [HttpGet("email/{id:guid}/stats")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetCampaignStats(
        Guid id,
        [FromQuery] Guid brandId,
        CancellationToken ct)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var stats = await mediator.Send(new GetCampaignStatsQuery(id, brandId), ct);

        if (stats is null)
            return NotFound(new { success = false, error = "Campaign not found." });

        return Ok(new { success = true, data = stats });
    }

    /// <summary>POST /api/distribution/email — create a campaign</summary>
    [HttpPost("email")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateCampaign(
        [FromBody] CreateCampaignDistRequest req,
        CancellationToken ct)
    {
        try
        {
            var id = await mediator.Send(
                new CreateEmailCampaignCommand(
                    req.BrandId,
                    req.Subject,
                    req.HtmlBody,
                    req.Name), ct);

            return CreatedAtAction(nameof(GetCampaigns),
                new { brandId = req.BrandId },
                new { success = true, data = new { id } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>POST /api/distribution/email/send — send a campaign to recipients</summary>
    [HttpPost("email/send")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SendCampaign(
        [FromBody] SendCampaignDistRequest req,
        CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(
                new SendEmailCampaignCommand(
                    req.CampaignId,
                    req.BrandId,
                    req.Recipients), ct);

            if (!result.Success && result.SentCount == 0)
                return BadRequest(new { success = false, error = result.ErrorMessage });

            return Ok(new
            {
                success = true,
                data = new
                {
                    result.SentCount,
                    result.FailedCount,
                    result.ErrorMessage,
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ─── Send Log ─────────────────────────────────────────────────────────

    /// <summary>GET /api/distribution/sendlog?brandId={id}&page=1&pageSize=50&channel={ch}</summary>
    [HttpGet("sendlog")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetSendLog(
        [FromQuery] Guid brandId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? channel = null,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var log = await mediator.Send(
            new GetSendLogQuery(brandId, page, pageSize, channel), ct);

        return Ok(new { success = true, data = log });
    }
}

// ─── Request models ───────────────────────────────────────────────────────────

public record SchedulePostRequest(
    Guid BrandId,
    string Platform,
    string Caption,
    DateTimeOffset ScheduledAt,
    string? ImageUrl = null,
    Guid? ContentPageId = null,
    string? Provider = null);

public record CreateCampaignDistRequest(
    Guid BrandId,
    string Subject,
    string HtmlBody,
    string? Name = null);

public record SendCampaignDistRequest(
    Guid CampaignId,
    Guid BrandId,
    List<string> Recipients);
