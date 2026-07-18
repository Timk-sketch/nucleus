using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.CmsRendererHub.DTOs;

namespace Nucleus.Application.CmsRendererHub.Queries;

/// <summary>
/// Returns the current deploy and cache status for a brand's CMS site.
/// Includes deploy history (most recent 20), published page count, and cached page count.
/// </summary>
public record GetSiteDeployStatusQuery(Guid BrandId) : IRequest<SiteStatusDto?>;

public class GetSiteDeployStatusHandler : IRequestHandler<GetSiteDeployStatusQuery, SiteStatusDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetSiteDeployStatusHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SiteStatusDto?> Handle(
        GetSiteDeployStatusQuery request, CancellationToken cancellationToken)
    {
        var brand = await _db.Brands
            .FirstOrDefaultAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId,
                cancellationToken);

        if (brand is null)
            return null;

        var publishedPageCount = await _db.WebsitePages
            .CountAsync(p => p.BrandId == request.BrandId && p.Status == "published",
                cancellationToken);

        var cachedPageCount = await _db.PageCaches
            .CountAsync(c => c.BrandId == request.BrandId && c.InvalidatedAt == null,
                cancellationToken);

        var deployments = await _db.SiteDeployments
            .Where(d => d.BrandId == request.BrandId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var deployDtos = deployments.Select(d => new SiteDeploymentDto
        {
            Id = d.Id,
            BrandId = d.BrandId,
            BrandName = brand.Name,
            DeployedBy = d.DeployedBy,
            PageCount = d.PageCount,
            Status = d.Status,
            DeployedAt = d.DeployedAt,
            Notes = d.Notes,
            CreatedAt = d.CreatedAt,
        }).ToList();

        return new SiteStatusDto
        {
            BrandId = brand.Id,
            BrandName = brand.Name,
            PublishedPageCount = publishedPageCount,
            CachedPageCount = cachedPageCount,
            LastDeployment = deployDtos.FirstOrDefault(),
            DeployHistory = deployDtos,
        };
    }
}
