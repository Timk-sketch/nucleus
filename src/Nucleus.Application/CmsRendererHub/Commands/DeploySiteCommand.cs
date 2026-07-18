using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.CmsRendererHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.CmsRendererHub.Commands;

/// <summary>
/// Deploys a brand's CMS site by snapshotting all published WebsitePages into PageCache.
/// Creates a SiteDeployment record tracking the deploy event.
/// Each published page's rendered HTML is written/updated in page_caches (upsert by BrandId+Slug).
/// Returns the SiteDeploymentDto for the new deployment record.
/// </summary>
public record DeploySiteCommand(Guid BrandId) : IRequest<SiteDeploymentDto>;

public class DeploySiteValidator : AbstractValidator<DeploySiteCommand>
{
    public DeploySiteValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty().WithMessage("BrandId is required.");
    }
}

public class DeploySiteHandler : IRequestHandler<DeploySiteCommand, SiteDeploymentDto>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public DeploySiteHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SiteDeploymentDto> Handle(
        DeploySiteCommand request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brand = await _db.Brands
            .FirstOrDefaultAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId,
                cancellationToken)
            ?? throw new InvalidOperationException("Brand not found for this tenant.");

        // Create the deployment record (status=running)
        var deployment = new SiteDeployment
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            DeployedBy = _tenant.UserId,
            Status = "running",
        };
        _db.SiteDeployments.Add(deployment);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            // Fetch all published pages for this brand
            var pages = await _db.WebsitePages
                .Where(p => p.BrandId == request.BrandId && p.Status == "published")
                .ToListAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;

            // Load existing cache entries for this brand (for upsert)
            var existingCaches = await _db.PageCaches
                .Where(c => c.BrandId == request.BrandId)
                .ToListAsync(cancellationToken);

            var cacheBySlug = existingCaches.ToDictionary(c => c.Slug, StringComparer.OrdinalIgnoreCase);

            foreach (var page in pages)
            {
                var renderedHtml = RenderPage(page, brand);
                var etag = ComputeEtag(renderedHtml);

                if (cacheBySlug.TryGetValue(page.Slug, out var existing))
                {
                    // Update existing cache entry
                    existing.RenderedHtml = renderedHtml;
                    existing.Etag = etag;
                    existing.CachedAt = now;
                    existing.InvalidatedAt = null;
                    existing.UpdatedAt = now;
                }
                else
                {
                    // Insert new cache entry
                    var entry = new PageCache
                    {
                        TenantId = _tenant.TenantId,
                        BrandId = request.BrandId,
                        Slug = page.Slug,
                        RenderedHtml = renderedHtml,
                        Etag = etag,
                        CachedAt = now,
                    };
                    _db.PageCaches.Add(entry);
                }
            }

            // Mark deployment complete
            deployment.Status = "complete";
            deployment.PageCount = pages.Count;
            deployment.DeployedAt = now;
            deployment.Notes = $"Deployed {pages.Count} page(s) successfully.";

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            deployment.Status = "failed";
            deployment.Notes = $"Deploy failed: {ex.Message}";
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }

        return new SiteDeploymentDto
        {
            Id = deployment.Id,
            BrandId = deployment.BrandId,
            BrandName = brand.Name,
            DeployedBy = deployment.DeployedBy,
            PageCount = deployment.PageCount,
            Status = deployment.Status,
            DeployedAt = deployment.DeployedAt,
            Notes = deployment.Notes,
            CreatedAt = deployment.CreatedAt,
        };
    }

    /// <summary>
    /// Renders the full HTML document for a published page.
    /// In production this would apply a theme template; here we wrap the page's
    /// stored HtmlContent in a complete HTML5 document with SEO metadata.
    /// </summary>
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

    /// <summary>Computes a short ETag from content (first 8 chars of MD5 hex).</summary>
    private static string ComputeEtag(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Security.Cryptography.MD5.HashData(bytes);
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
