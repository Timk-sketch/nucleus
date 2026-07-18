using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nucleus.Application.AuthorityHub.Commands;
using Nucleus.Application.AuthorityHub.Queries;

namespace Nucleus.Api.Controllers;

/// <summary>
/// Authority Hub endpoints — backlinks, brand mentions, schema templates, outreach queue.
/// All endpoints are tenant-scoped via JWT claims (ICurrentTenantService injected in handlers).
///
/// Backlinks:  GET  /api/authority/backlinks?brandId=&amp;activeOnly=&amp;page=&amp;pageSize=
///             POST /api/authority/backlinks/sync
/// Mentions:   GET  /api/authority/mentions?brandId=&amp;unreviewedOnly=&amp;sentiment=&amp;page=
///             PUT  /api/authority/mentions/{id}/reviewed
/// Schema:     GET  /api/authority/schema?brandId=&amp;pageType=&amp;activeOnly=
///             POST /api/authority/schema
/// Outreach:   GET  /api/authority/outreach?brandId=&amp;status=&amp;page=
///             POST /api/authority/outreach
///             POST /api/authority/outreach/{id}/send
/// </summary>
[ApiController]
[Route("api/authority")]
[Authorize]
[Produces("application/json")]
public class AuthorityController(IMediator mediator) : ControllerBase
{
    // ─── Backlinks ────────────────────────────────────────────────────────

    /// <summary>GET /api/authority/backlinks?brandId={id}&activeOnly=false&page=1&pageSize=50</summary>
    [HttpGet("backlinks")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetBacklinks(
        [FromQuery] Guid brandId,
        [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var result = await mediator.Send(
            new GetBacklinkProfileQuery(brandId, activeOnly, page, pageSize), ct);

        if (result is null)
            return NotFound(new { success = false, error = "Brand not found." });

        return Ok(new { success = true, data = result });
    }

    /// <summary>POST /api/authority/backlinks/sync — upsert a batch of backlinks (called by Hangfire job)</summary>
    [HttpPost("backlinks/sync")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SyncBacklinks(
        [FromBody] SyncBacklinksRequest req,
        CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(
                new SyncBacklinksCommand(
                    req.BrandId,
                    req.Backlinks.Select(b => new BacklinkInput(
                        b.SourceUrl,
                        b.TargetUrl,
                        b.AnchorText,
                        b.DomainRating,
                        b.IsActive)).ToList()),
                ct);

            return Ok(new
            {
                success = true,
                data = new { result.Added, result.Updated, result.Total }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ─── Brand Mentions ───────────────────────────────────────────────────

    /// <summary>GET /api/authority/mentions?brandId={id}&unreviewedOnly=false&sentiment=&page=1&pageSize=50</summary>
    [HttpGet("mentions")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetMentions(
        [FromQuery] Guid brandId,
        [FromQuery] bool unreviewedOnly = false,
        [FromQuery] string? sentiment = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var mentions = await mediator.Send(
            new GetBrandMentionsQuery(brandId, unreviewedOnly, sentiment, page, pageSize), ct);

        return Ok(new { success = true, data = mentions });
    }

    /// <summary>PUT /api/authority/mentions/{id}/reviewed — mark a mention as reviewed</summary>
    [HttpPut("mentions/{id:guid}/reviewed")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> MarkMentionReviewed(
        Guid id,
        [FromBody] MarkReviewedRequest? req = null,
        CancellationToken ct = default)
    {
        var isReviewed = req?.IsReviewed ?? true;
        var found = await mediator.Send(new MarkMentionReviewedCommand(id, isReviewed), ct);
        return found ? NoContent() : NotFound(new { success = false, error = "Mention not found." });
    }

    // ─── Schema Templates ─────────────────────────────────────────────────

    /// <summary>GET /api/authority/schema?brandId={id}&pageType=&activeOnly=false</summary>
    [HttpGet("schema")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetSchemaLibrary(
        [FromQuery] Guid brandId,
        [FromQuery] string? pageType = null,
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var templates = await mediator.Send(
            new GetSchemaLibraryQuery(brandId, pageType, activeOnly), ct);

        return Ok(new { success = true, data = templates });
    }

    /// <summary>POST /api/authority/schema — create a schema template</summary>
    [HttpPost("schema")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateSchemaTemplate(
        [FromBody] CreateSchemaRequest req,
        CancellationToken ct)
    {
        try
        {
            var id = await mediator.Send(
                new CreateSchemaTemplateCommand(
                    req.BrandId,
                    req.PageType,
                    req.SchemaType,
                    req.TemplateJson,
                    req.IsActive ?? true),
                ct);

            return CreatedAtAction(nameof(GetSchemaLibrary),
                new { brandId = req.BrandId },
                new { success = true, data = new { id } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ─── Outreach Queue ───────────────────────────────────────────────────

    /// <summary>GET /api/authority/outreach?brandId={id}&status=&page=1&pageSize=50</summary>
    [HttpGet("outreach")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetOutreachQueue(
        [FromQuery] Guid brandId,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var items = await mediator.Send(
            new GetOutreachQueueQuery(brandId, status, page, pageSize), ct);

        return Ok(new { success = true, data = items });
    }

    /// <summary>POST /api/authority/outreach — add a prospect to the outreach queue</summary>
    [HttpPost("outreach")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> AddOutreachItem(
        [FromBody] AddOutreachRequest req,
        CancellationToken ct)
    {
        try
        {
            var id = await mediator.Send(
                new AddOutreachItemCommand(req.BrandId, req.TargetUrl, req.ContactEmail, req.Notes),
                ct);

            return CreatedAtAction(nameof(GetOutreachQueue),
                new { brandId = req.BrandId },
                new { success = true, data = new { id } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>POST /api/authority/outreach/{id}/send — send outreach email for a queue item</summary>
    [HttpPost("outreach/{id:guid}/send")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SendOutreach(
        Guid id,
        [FromBody] SendOutreachRequest req,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new SendOutreachCommand(id, req.Subject, req.HtmlBody), ct);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("not found") == true)
                return NotFound(new { success = false, error = result.ErrorMessage });
            return BadRequest(new { success = false, error = result.ErrorMessage });
        }

        return Ok(new { success = true, message = "Outreach email sent." });
    }
}

// ─── Request models ───────────────────────────────────────────────────────────

public record SyncBacklinksRequest(Guid BrandId, List<BacklinkInputDto> Backlinks);

public record BacklinkInputDto(
    string SourceUrl,
    string TargetUrl,
    string? AnchorText = null,
    decimal? DomainRating = null,
    bool IsActive = true);

public record MarkReviewedRequest(bool IsReviewed = true);

public record CreateSchemaRequest(
    Guid BrandId,
    string PageType,
    string SchemaType,
    string? TemplateJson = null,
    bool? IsActive = null);

public record AddOutreachRequest(
    Guid BrandId,
    string TargetUrl,
    string? ContactEmail = null,
    string? Notes = null);

public record SendOutreachRequest(string Subject, string HtmlBody);
