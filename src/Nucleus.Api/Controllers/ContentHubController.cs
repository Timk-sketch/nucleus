using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nucleus.Application.ContentHub.Commands;
using Nucleus.Application.ContentHub.Queries;

namespace Nucleus.Api.Controllers;

/// <summary>
/// Content Hub endpoints — keyword library, AI generator, editorial calendar,
/// content library, approval queue, brand voice, templates.
///
/// All endpoints are tenant-scoped via JWT claims (ICurrentTenantService injected in handlers).
///
/// Keywords:   GET  /api/content/keywords?brandId=&search=&page=&pageSize=
/// Generator:  POST /api/content/generate
/// Library:    GET  /api/content/library?brandId=&status=&pageType=&search=&page=
///             POST /api/content/pages (manual create)
/// Calendar:   GET  /api/content/calendar?brandId=&windowStart=&windowEnd=
/// Queue:      GET  /api/content/queue?brandId=
///             PUT  /api/content/pages/{id}/approve
/// BrandVoice: GET  /api/content/brand-voice?brandId=
///             POST /api/content/brand-voice/banned-words
///             DELETE /api/content/brand-voice/banned-words/{id}
/// Templates:  GET  /api/content/templates?brandId=&pageType=
///             POST /api/content/templates
/// </summary>
[ApiController]
[Route("api/content")]
[Authorize]
[Produces("application/json")]
public class ContentHubController(IMediator mediator) : ControllerBase
{
    // ─── Keyword Library ──────────────────────────────────────────────────

    /// <summary>GET /api/content/keywords?brandId={id}&search=&page=1&pageSize=50</summary>
    [HttpGet("keywords")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetKeywords(
        [FromQuery] Guid brandId,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var result = await mediator.Send(
            new GetKeywordLibraryQuery(brandId, search, page, pageSize), ct);

        if (result is null)
            return NotFound(new { success = false, error = "Brand not found." });

        return Ok(new { success = true, data = result });
    }

    // ─── AI Generator ─────────────────────────────────────────────────────

    /// <summary>POST /api/content/generate — AI-generate a content page</summary>
    [HttpPost("generate")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(402)]
    public async Task<IActionResult> GenerateContent(
        [FromBody] GenerateContentRequest req,
        CancellationToken ct)
    {
        try
        {
            var result = await mediator.Send(
                new GenerateContentCommand(
                    req.BrandId,
                    req.Title,
                    req.PageType,
                    req.FocusKeyword,
                    req.KeywordId,
                    req.WordCount,
                    req.CustomPrompt,
                    req.TemplateId),
                ct);

            if (!result.Success)
            {
                if (result.PlanLimitReached)
                    return StatusCode(402, new { success = false, error = result.ErrorMessage });

                return BadRequest(new { success = false, error = result.ErrorMessage });
            }

            return CreatedAtAction(nameof(GetLibrary),
                new { brandId = req.BrandId },
                new { success = true, data = result.ContentPage });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ─── Content Library ──────────────────────────────────────────────────

    /// <summary>GET /api/content/library?brandId={id}&status=&pageType=&search=&page=1&pageSize=20</summary>
    [HttpGet("library")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetLibrary(
        [FromQuery] Guid brandId,
        [FromQuery] string? status = null,
        [FromQuery] string? pageType = null,
        [FromQuery] Guid? keywordId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var result = await mediator.Send(
            new GetContentLibraryQuery(brandId, status, pageType, keywordId, search, page, pageSize), ct);

        if (result is null)
            return NotFound(new { success = false, error = "Brand not found." });

        return Ok(new { success = true, data = result });
    }

    /// <summary>POST /api/content/pages — manually create a content page</summary>
    [HttpPost("pages")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreatePage(
        [FromBody] CreateContentPageRequest req,
        CancellationToken ct)
    {
        try
        {
            var id = await mediator.Send(
                new CreateContentPageCommand(
                    req.BrandId,
                    req.Title,
                    req.PageType,
                    req.KeywordId,
                    req.HtmlContent,
                    req.SeoTitle,
                    req.MetaDescription,
                    req.ScheduledAt),
                ct);

            return CreatedAtAction(nameof(GetLibrary),
                new { brandId = req.BrandId },
                new { success = true, data = new { id } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ─── Editorial Calendar ────────────────────────────────────────────────

    /// <summary>GET /api/content/calendar?brandId={id}&windowStart=&windowEnd=</summary>
    [HttpGet("calendar")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetCalendar(
        [FromQuery] Guid brandId,
        [FromQuery] DateTimeOffset? windowStart = null,
        [FromQuery] DateTimeOffset? windowEnd = null,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var result = await mediator.Send(
            new GetEditorialCalendarQuery(brandId, windowStart, windowEnd), ct);

        if (result is null)
            return NotFound(new { success = false, error = "Brand not found." });

        return Ok(new { success = true, data = result });
    }

    // ─── Approval Queue ────────────────────────────────────────────────────

    /// <summary>GET /api/content/queue?brandId={id}&includeRecent=true</summary>
    [HttpGet("queue")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetApprovalQueue(
        [FromQuery] Guid brandId,
        [FromQuery] bool includeRecent = true,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var result = await mediator.Send(
            new GetContentApprovalQueueQuery(brandId, includeRecent), ct);

        if (result is null)
            return NotFound(new { success = false, error = "Brand not found." });

        return Ok(new { success = true, data = result });
    }

    /// <summary>PUT /api/content/pages/{id}/approve — approve or reject a content page</summary>
    [HttpPut("pages/{id:guid}/approve")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ApprovePage(
        Guid id,
        [FromBody] ApprovePageRequest req,
        CancellationToken ct)
    {
        var found = await mediator.Send(
            new ApproveContentPageCommand(id, req.Approve, req.ReviewNotes), ct);

        return found
            ? NoContent()
            : NotFound(new { success = false, error = "Content page not found." });
    }

    // ─── Brand Voice ──────────────────────────────────────────────────────

    /// <summary>GET /api/content/brand-voice?brandId={id}</summary>
    [HttpGet("brand-voice")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetBrandVoice(
        [FromQuery] Guid brandId,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var result = await mediator.Send(new GetBrandVoiceQuery(brandId), ct);

        if (result is null)
            return NotFound(new { success = false, error = "Brand not found." });

        return Ok(new { success = true, data = result });
    }

    /// <summary>POST /api/content/brand-voice/banned-words — add a banned word</summary>
    [HttpPost("brand-voice/banned-words")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> AddBannedWord(
        [FromBody] AddBannedWordRequest req,
        CancellationToken ct)
    {
        try
        {
            var dto = await mediator.Send(
                new AddBannedWordCommand(req.BrandId, req.Word, req.Reason), ct);

            return CreatedAtAction(nameof(GetBrandVoice),
                new { brandId = req.BrandId },
                new { success = true, data = dto });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ─── Content Templates ────────────────────────────────────────────────

    /// <summary>GET /api/content/templates?brandId={id}&pageType=&activeOnly=true</summary>
    [HttpGet("templates")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetTemplates(
        [FromQuery] Guid brandId,
        [FromQuery] string? pageType = null,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var templates = await mediator.Send(
            new GetContentTemplatesQuery(brandId, pageType, activeOnly), ct);

        return Ok(new { success = true, data = templates });
    }

    /// <summary>POST /api/content/templates — create a content template</summary>
    [HttpPost("templates")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateTemplate(
        [FromBody] CreateTemplateRequest req,
        CancellationToken ct)
    {
        try
        {
            var dto = await mediator.Send(
                new CreateContentTemplateCommand(
                    req.BrandId,
                    req.Name,
                    req.PageType,
                    req.Body,
                    req.IsGlobal,
                    req.IsActive ?? true),
                ct);

            return CreatedAtAction(nameof(GetTemplates),
                new { brandId = req.BrandId },
                new { success = true, data = dto });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
}

// ─── Request models ───────────────────────────────────────────────────────────

public record GenerateContentRequest(
    Guid BrandId,
    string Title,
    string PageType,
    string? FocusKeyword = null,
    Guid? KeywordId = null,
    int WordCount = 800,
    string? CustomPrompt = null,
    Guid? TemplateId = null);

public record CreateContentPageRequest(
    Guid BrandId,
    string Title,
    string PageType,
    Guid? KeywordId = null,
    string? HtmlContent = null,
    string? SeoTitle = null,
    string? MetaDescription = null,
    DateTimeOffset? ScheduledAt = null);

public record ApprovePageRequest(bool Approve, string? ReviewNotes = null);

public record AddBannedWordRequest(Guid BrandId, string Word, string? Reason = null);

public record CreateTemplateRequest(
    Guid BrandId,
    string Name,
    string PageType,
    string Body,
    bool IsGlobal = false,
    bool? IsActive = null);
