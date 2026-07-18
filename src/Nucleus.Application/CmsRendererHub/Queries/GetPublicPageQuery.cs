using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.CmsRendererHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.CmsRendererHub.Queries;

/// <summary>
/// Core CMS renderer query.
/// Looks up a published WebsitePage by BrandId + Slug.
/// Checks PageCache first (cache hit path target: &lt;5ms).
/// On cache miss: renders the page HTML + writes to PageCache.
/// Also logs a SiteVisit record for analytics.
/// Returns null if the page doesn't exist or is not published.
///
/// IMPORTANT: This query uses IgnoreQueryFilters() on PageCache because the BrandId
/// is resolved from the Host header (not from the current authenticated tenant).
/// The public renderer is unauthenticated; BrandId is the security boundary here.
/// </summary>
public record GetPublicPageQuery(
    Guid BrandId,
    string Slug,
    string? Referrer = null,
    string? UserAgent = null,
    string? IpHash = null) : IRequest<PublicPageDto?>;

public class GetPublicPageHandler : IRequestHandler<GetPublicPageQuery, PublicPageDto?>
{
    private readonly INucleusDbContext _db;

    public GetPublicPageHandler(INucleusDbContext db)
    {
        _db = db;
    }

    public async Task<PublicPageDto?> Handle(
        GetPublicPageQuery request, CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim().ToLowerInvariant().TrimStart('/');

        // ── 1. Check PageCache (fast path) ─────────────────────────────
        var cached = await _db.PageCaches
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                c => c.BrandId == request.BrandId
                     && c.Slug == slug
                     && c.InvalidatedAt == null,
                cancellationToken);

        if (cached is not null)
        {
            // Log visit asynchronously (fire-and-forget in the same request, no await)
            await LogVisitAsync(request, slug, cancellationToken);

            return new PublicPageDto
            {
                Slug = cached.Slug,
                Title = slug,          // minimal — cache stores full HTML
                RenderedHtml = cached.RenderedHtml,
                Etag = cached.Etag,
                CachedAt = cached.CachedAt,
                ServedFromCache = true,
            };
        }

        // ── 2. Cache miss — look up the published page ──────────────────
        var page = await _db.WebsitePages
            .IgnoreQueryFilters()
            .Include(p => p.Brand)
            .FirstOrDefaultAsync(
                p => p.BrandId == request.BrandId
                     && p.Slug == slug
                     && p.Status == "published",
                cancellationToken);

        if (page is null)
            return null;

        // ── 3. Render the page ──────────────────────────────────────────
        var renderedHtml = RenderPage(page, page.Brand);
        var etag = ComputeEtag(renderedHtml);
        var now = DateTimeOffset.UtcNow;

        // ── 4. Write to PageCache ───────────────────────────────────────
        var cacheEntry = new PageCache
        {
            TenantId = page.TenantId,
            BrandId = request.BrandId,
            Slug = slug,
            RenderedHtml = renderedHtml,
            Etag = etag,
            CachedAt = now,
        };
        _db.PageCaches.Add(cacheEntry);

        // ── 5. Log visit ────────────────────────────────────────────────
        await LogVisitAsync(request, slug, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        return new PublicPageDto
        {
            Slug = page.Slug,
            Title = page.Title,
            SeoTitle = page.SeoTitle,
            MetaDescription = page.MetaDescription,
            OgImage = page.OgImage,
            SchemaJson = page.SchemaJson,
            RenderedHtml = renderedHtml,
            Etag = etag,
            CachedAt = now,
            ServedFromCache = false,
        };
    }

    private async Task LogVisitAsync(GetPublicPageQuery request, string slug, CancellationToken ct)
    {
        try
        {
            // Load brand's tenantId for the visit record
            var brand = await _db.Brands
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(b => b.Id == request.BrandId, ct);

            if (brand is null) return;

            var visit = new SiteVisit
            {
                TenantId = brand.TenantId,
                BrandId = request.BrandId,
                Slug = slug,
                Referrer = request.Referrer?.Length > 1000
                    ? request.Referrer[..1000]
                    : request.Referrer,
                UserAgent = request.UserAgent?.Length > 500
                    ? request.UserAgent[..500]
                    : request.UserAgent,
                IpHash = request.IpHash,
                VisitedAt = DateTimeOffset.UtcNow,
            };
            _db.SiteVisits.Add(visit);
        }
        catch
        {
            // Visit logging must never block page serving
        }
    }

    private static string RenderPage(WebsitePage page, Brand brand)
    {
        var title = page.SeoTitle ?? page.Title;
        var description = page.MetaDescription ?? string.Empty;
        var ogImage = page.OgImage ?? string.Empty;
        var body = page.HtmlContent ?? $"<h1>{System.Net.WebUtility.HtmlEncode(page.Title)}</h1>";
        var schemaBlock = !string.IsNullOrWhiteSpace(page.SchemaJson)
            ? $"<script type=\"application/ld+json\">{page.SchemaJson}</script>"
            : string.Empty;

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>{System.Net.WebUtility.HtmlEncode(title)}</title>
              <meta name="description" content="{System.Net.WebUtility.HtmlEncode(description)}" />
              <meta property="og:title" content="{System.Net.WebUtility.HtmlEncode(title)}" />
              <meta property="og:description" content="{System.Net.WebUtility.HtmlEncode(description)}" />
              {(string.IsNullOrEmpty(ogImage) ? "" : $"<meta property=\"og:image\" content=\"{ogImage}\" />")}
              {schemaBlock}
            </head>
            <body>
            {body}
            </body>
            </html>
            """;
    }

    private static string ComputeEtag(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Security.Cryptography.MD5.HashData(bytes);
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
