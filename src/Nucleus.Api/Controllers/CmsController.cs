using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nucleus.Application.CmsRendererHub.Commands;
using Nucleus.Application.CmsRendererHub.Queries;

namespace Nucleus.Api.Controllers;

/// <summary>
/// CMS Renderer Hub — two responsibilities:
///
/// 1. PUBLIC renderer (no auth required):
///    GET /cms/{*slug}  — resolves brand from Host header, serves rendered HTML or 404.
///    The public endpoint bypasses [Authorize] entirely so search engines and visitors
///    can reach pages without a JWT.
///
/// 2. MANAGEMENT API (auth required):
///    POST /api/cms/deploy                     — deploy site (warm PageCache for all published pages)
///    POST /api/cms/cache/invalidate           — invalidate a specific slug cache entry
///    GET  /api/cms/status?brandId=            — site deploy status + history
///    GET  /api/cms/analytics?brandId=&amp;days=   — site visit analytics
///    GET  /api/cms/domains?brandId=           — list custom domains
///    POST /api/cms/domains                    — map custom domain
///    POST /api/cms/domains/{id}/verify        — verify domain DNS
/// </summary>
[ApiController]
[Produces("application/json")]
public class CmsController(IMediator mediator) : ControllerBase
{
    // ─── Public Page Renderer ─────────────────────────────────────────────
    // NOTE: No [Authorize] — public web pages must be accessible without login.

    /// <summary>
    /// GET /cms/{*slug} — public page renderer.
    /// Resolves the brand from the Host header via SiteDomain table lookup.
    /// Returns 200 text/html on success, 404 on unknown slug or unpublished page.
    /// </summary>
    [HttpGet("/cms/{*slug}")]
    [AllowAnonymous]
    [Produces("text/html", "application/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ServePage(
        string slug,
        CancellationToken ct = default)
    {
        // ── Resolve brand from Host header ──────────────────────────────
        var hostname = Request.Host.Host.ToLowerInvariant();

        var domainResult = await mediator.Send(
            new GetCustomDomainQuery(Hostname: hostname), ct);

        if (domainResult.ResolvedBrandId is null)
        {
            // No domain mapping — return 404 JSON for API consumers
            return NotFound(new { success = false, error = $"No site mapped to host '{hostname}'." });
        }

        var brandId = domainResult.ResolvedBrandId.Value;

        // ── Collect analytics metadata ──────────────────────────────────
        var referrer = Request.Headers["Referer"].FirstOrDefault();
        var userAgent = Request.Headers["User-Agent"].FirstOrDefault();
        var ipHash = HashIp(Request.HttpContext.Connection.RemoteIpAddress?.ToString());

        // ── Get (or render+cache) the page ──────────────────────────────
        var page = await mediator.Send(
            new GetPublicPageQuery(brandId, slug, referrer, userAgent, ipHash), ct);

        if (page is null)
            return NotFound(new { success = false, error = $"Page '/{slug}' not found or not published." });

        // ── Return HTML with ETag caching headers ───────────────────────
        Response.Headers.ETag = $"\"{page.Etag}\"";
        Response.Headers["Cache-Control"] = "public, max-age=300"; // 5-min browser cache

        // Support conditional GET (If-None-Match)
        var requestEtag = Request.Headers.IfNoneMatch.FirstOrDefault()?.Trim('"');
        if (!string.IsNullOrEmpty(requestEtag) && requestEtag == page.Etag)
            return StatusCode(304);

        return Content(page.RenderedHtml, "text/html; charset=utf-8");
    }

    // ─── Site Management (Authenticated) ─────────────────────────────────

    /// <summary>POST /api/cms/deploy — deploy site, warm PageCache for all published pages</summary>
    [HttpPost("/api/cms/deploy")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> DeploySite(
        [FromBody] DeploySiteRequest req,
        CancellationToken ct)
    {
        try
        {
            var deployment = await mediator.Send(new DeploySiteCommand(req.BrandId), ct);
            return Ok(new { success = true, data = deployment });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>POST /api/cms/cache/invalidate — invalidate a specific slug's cache entry</summary>
    [HttpPost("/api/cms/cache/invalidate")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> InvalidateCache(
        [FromBody] InvalidateCacheRequest req,
        CancellationToken ct)
    {
        try
        {
            var found = await mediator.Send(
                new InvalidatePageCacheCommand(req.BrandId, req.Slug), ct);

            if (!found)
                return NotFound(new { success = false, error = $"No cache entry found for slug '{req.Slug}'." });

            return Ok(new { success = true, message = $"Cache invalidated for '/{req.Slug}'." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>GET /api/cms/status?brandId={id} — site deploy status + history</summary>
    [HttpGet("/api/cms/status")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetStatus(
        [FromQuery] Guid brandId,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var status = await mediator.Send(new GetSiteDeployStatusQuery(brandId), ct);

        if (status is null)
            return NotFound(new { success = false, error = "Brand not found." });

        return Ok(new { success = true, data = status });
    }

    /// <summary>GET /api/cms/analytics?brandId={id}&amp;days=30 — visit analytics</summary>
    [HttpGet("/api/cms/analytics")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetAnalytics(
        [FromQuery] Guid brandId,
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var analytics = await mediator.Send(new GetSiteAnalyticsQuery(brandId, days), ct);

        if (analytics is null)
            return NotFound(new { success = false, error = "Brand not found." });

        return Ok(new { success = true, data = analytics });
    }

    // ─── Domain Management ────────────────────────────────────────────────

    /// <summary>GET /api/cms/domains?brandId={id} — list custom domains for a brand</summary>
    [HttpGet("/api/cms/domains")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetDomains(
        [FromQuery] Guid brandId,
        CancellationToken ct = default)
    {
        if (brandId == Guid.Empty)
            return BadRequest(new { success = false, error = "brandId is required." });

        var result = await mediator.Send(new GetCustomDomainQuery(BrandId: brandId), ct);
        return Ok(new { success = true, data = result.Domains });
    }

    /// <summary>POST /api/cms/domains — map a custom domain to a brand</summary>
    [HttpPost("/api/cms/domains")]
    [Authorize]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> MapDomain(
        [FromBody] MapDomainRequest req,
        CancellationToken ct)
    {
        try
        {
            var domain = await mediator.Send(
                new MapCustomDomainCommand(req.BrandId, req.Hostname, req.IsPrimary ?? false), ct);

            return CreatedAtAction(nameof(GetDomains),
                new { brandId = req.BrandId },
                new { success = true, data = domain });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>POST /api/cms/domains/{id}/verify — trigger DNS verification for a domain</summary>
    [HttpPost("/api/cms/domains/{id:guid}/verify")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> VerifyDomain(Guid id, CancellationToken ct)
    {
        var domain = await mediator.Send(new VerifyDomainCommand(id), ct);

        if (domain is null)
            return NotFound(new { success = false, error = "Domain not found." });

        return Ok(new { success = true, data = domain });
    }

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static string? HashIp(string? ip)
    {
        if (string.IsNullOrEmpty(ip)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

// ─── Request models ───────────────────────────────────────────────────────────

public record DeploySiteRequest(Guid BrandId);

public record InvalidateCacheRequest(Guid BrandId, string Slug);

public record MapDomainRequest(Guid BrandId, string Hostname, bool? IsPrimary = null);
