using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.StudioHub.DTOs;

namespace Nucleus.Application.StudioHub.Queries;

/// <summary>
/// Returns the Design Studio context for a brand.
/// Aggregates brand identity (colors, domain), recent pages, and recent assets
/// so the Studio UI can provide rich AI-generation context.
/// Also returns overall Studio stats for the hub dashboard.
/// </summary>
public record GetDesignStudioContextQuery(Guid BrandId) : IRequest<DesignStudioContextDto?>;

public class GetDesignStudioContextHandler : IRequestHandler<GetDesignStudioContextQuery, DesignStudioContextDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetDesignStudioContextHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<DesignStudioContextDto?> Handle(
        GetDesignStudioContextQuery request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brand = await _db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId)
            .Select(b => new { b.Id, b.Name, b.Domain, b.PrimaryColor })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null) return null;

        // Recent pages (last 10 for context)
        var recentPages = await _db.WebsitePages
            .Where(p => p.BrandId == request.BrandId)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(10)
            .Select(p => new PageSummary
            {
                Id = p.Id,
                Slug = p.Slug,
                Title = p.Title,
                PageType = p.PageType,
                Status = p.Status,
            })
            .ToListAsync(cancellationToken);

        // Recent assets (last 10 for context)
        var recentAssets = await _db.DesignAssets
            .Where(a => a.BrandId == request.BrandId)
            .OrderByDescending(a => a.UploadedAt)
            .Take(10)
            .Select(a => new AssetSummary
            {
                Id = a.Id,
                Name = a.Name,
                AssetType = a.AssetType,
                Url = a.Url,
            })
            .ToListAsync(cancellationToken);

        // Studio stats
        var pageStats = await _db.WebsitePages
            .Where(p => p.BrandId == request.BrandId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Published = g.Count(p => p.Status == "published"),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var assetStats = await _db.DesignAssets
            .Where(a => a.BrandId == request.BrandId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Generated = g.Count(a => a.AssetType == "generated"),
            })
            .FirstOrDefaultAsync(cancellationToken);

        var videoCount = await _db.VideoAssets
            .CountAsync(v => v.BrandId == request.BrandId, cancellationToken);

        return new DesignStudioContextDto
        {
            BrandId = brand.Id,
            BrandName = brand.Name,
            Domain = brand.Domain,
            PrimaryColor = brand.PrimaryColor,
            RecentPages = recentPages,
            RecentAssets = recentAssets,
            Stats = new StudioStats
            {
                TotalPages = pageStats?.Total ?? 0,
                PublishedPages = pageStats?.Published ?? 0,
                TotalAssets = assetStats?.Total ?? 0,
                GeneratedImages = assetStats?.Generated ?? 0,
                TotalVideos = videoCount,
            },
        };
    }
}
