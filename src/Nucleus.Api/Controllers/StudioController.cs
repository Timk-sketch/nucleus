using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nucleus.Application.StudioHub.Commands;
using Nucleus.Application.StudioHub.Queries;

namespace Nucleus.Api.Controllers;

/// <summary>
/// Studio Hub endpoints — page manager, design studio, image generator, asset library.
/// All endpoints are tenant-scoped via JWT claims (ICurrentTenantService injected in handlers).
///
/// Pages:    GET  /api/studio/pages?brandId=&amp;status=&amp;pageType=&amp;page=&amp;pageSize=
///           POST /api/studio/pages
///           PUT  /api/studio/pages/{id}/publish
///           PUT  /api/studio/pages/{id}/unpublish
/// Design:   POST /api/studio/design/generate
///           GET  /api/studio/design/context?brandId=
/// Images:   POST /api/studio/images/generate
/// Assets:   GET  /api/studio/assets?brandId=&amp;assetType=&amp;page=&amp;pageSize=
///           POST /api/studio/assets
/// </summary>
[ApiController]
[Route("api/studio")]
[Authorize]
[Produces("application/json")]
public class StudioController(IMediator mediator) : ControllerBase
{
    // ─── Page Manager ─────────────────────────────────────────────────────

    /// <summary>GET /api/studio/pages?brandId={id}&amp;status=draft&amp;pageType=&amp;page=1&amp;pageSize=50</summary>
    [HttpGet("pages")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPages(
        [FromQuery] Guid brandId,
        [FromQuery] string? status = null,
        [FromQuery] string? pageType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var result = await mediator.Send(
            new GetPageLibraryQuery(brandId, status, pageType, page, pageSize), ct);

        if (result is null)
            return NotFound(new { success = false, error = "Brand not found." });

        return Ok(new { success = true, data = result });
    }

    /// <summary>POST /api/studio/pages — create a new CMS page (draft)</summary>
    [HttpPost("pages")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreatePage(
        [FromBody] CreatePageRequest req,
        CancellationToken ct)
    {
        try
        {
            var id = await mediator.Send(
                new CreateWebsitePageCommand(
                    req.BrandId,
                    req.Slug,
                    req.Title,
                    req.PageType,
                    req.HtmlContent,
                    req.SeoTitle,
                    req.MetaDescription,
                    req.OgImage,
                    req.SchemaJson),
                ct);

            return CreatedAtAction(nameof(GetPages),
                new { brandId = req.BrandId },
                new { success = true, data = new { id } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>PUT /api/studio/pages/{id}/publish — set status = published</summary>
    [HttpPut("pages/{id:guid}/publish")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> PublishPage(Guid id, CancellationToken ct)
    {
        var page = await mediator.Send(new PublishWebsitePageCommand(id, Publish: true), ct);

        if (page is null)
            return NotFound(new { success = false, error = "Page not found." });

        return Ok(new { success = true, data = page });
    }

    /// <summary>PUT /api/studio/pages/{id}/unpublish — revert to draft</summary>
    [HttpPut("pages/{id:guid}/unpublish")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UnpublishPage(Guid id, CancellationToken ct)
    {
        var page = await mediator.Send(new PublishWebsitePageCommand(id, Publish: false), ct);

        if (page is null)
            return NotFound(new { success = false, error = "Page not found." });

        return Ok(new { success = true, data = page });
    }

    // ─── Design Studio ────────────────────────────────────────────────────

    /// <summary>GET /api/studio/design/context?brandId={id} — studio context + stats</summary>
    [HttpGet("design/context")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetDesignContext(
        [FromQuery] Guid brandId,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var ctx = await mediator.Send(new GetDesignStudioContextQuery(brandId), ct);

        if (ctx is null)
            return NotFound(new { success = false, error = "Brand not found." });

        return Ok(new { success = true, data = ctx });
    }

    /// <summary>POST /api/studio/design/generate — AI-generate an HTML page scaffold</summary>
    [HttpPost("design/generate")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GenerateDesign(
        [FromBody] GenerateDesignRequest req,
        CancellationToken ct)
    {
        try
        {
            var page = await mediator.Send(
                new GenerateDesignCommand(req.BrandId, req.PageType, req.Prompt, req.TargetSlug), ct);

            return CreatedAtAction(nameof(GetPages),
                new { brandId = req.BrandId },
                new { success = true, data = page });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ─── Image Generator ──────────────────────────────────────────────────

    /// <summary>POST /api/studio/images/generate — Flux image generation</summary>
    [HttpPost("images/generate")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GenerateImage(
        [FromBody] GenerateImageRequest req,
        CancellationToken ct)
    {
        try
        {
            var asset = await mediator.Send(
                new GenerateImageCommand(
                    req.BrandId,
                    req.Prompt,
                    req.StyleHint,
                    req.Width ?? 1024,
                    req.Height ?? 1024),
                ct);

            return CreatedAtAction(nameof(GetAssets),
                new { brandId = req.BrandId, assetType = "generated" },
                new { success = true, data = asset });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    // ─── Asset Library ────────────────────────────────────────────────────

    /// <summary>GET /api/studio/assets?brandId={id}&amp;assetType=image&amp;page=1&amp;pageSize=50</summary>
    [HttpGet("assets")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAssets(
        [FromQuery] Guid brandId,
        [FromQuery] string? assetType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var result = await mediator.Send(
            new GetAssetLibraryQuery(brandId, assetType, page, pageSize), ct);

        if (result is null)
            return NotFound(new { success = false, error = "Brand not found." });

        return Ok(new { success = true, data = result });
    }

    /// <summary>POST /api/studio/assets — register an uploaded asset in the library</summary>
    [HttpPost("assets")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UploadAsset(
        [FromBody] UploadAssetRequest req,
        CancellationToken ct)
    {
        try
        {
            var asset = await mediator.Send(
                new UploadAssetCommand(
                    req.BrandId,
                    req.Name,
                    req.AssetType,
                    req.Url,
                    req.Width,
                    req.Height,
                    req.FileSize,
                    req.MimeType,
                    req.PromptUsed),
                ct);

            return CreatedAtAction(nameof(GetAssets),
                new { brandId = req.BrandId },
                new { success = true, data = asset });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
}

// ─── Request models ───────────────────────────────────────────────────────────

public record CreatePageRequest(
    Guid BrandId,
    string Slug,
    string Title,
    string PageType,
    string? HtmlContent = null,
    string? SeoTitle = null,
    string? MetaDescription = null,
    string? OgImage = null,
    string? SchemaJson = null);

public record GenerateDesignRequest(
    Guid BrandId,
    string PageType,
    string Prompt,
    string? TargetSlug = null);

public record GenerateImageRequest(
    Guid BrandId,
    string Prompt,
    string? StyleHint = null,
    int? Width = null,
    int? Height = null);

public record UploadAssetRequest(
    Guid BrandId,
    string Name,
    string AssetType,
    string Url,
    int? Width = null,
    int? Height = null,
    long? FileSize = null,
    string? MimeType = null,
    string? PromptUsed = null);
