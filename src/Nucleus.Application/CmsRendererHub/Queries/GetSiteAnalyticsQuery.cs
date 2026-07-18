using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.CmsRendererHub.DTOs;

namespace Nucleus.Application.CmsRendererHub.Queries;

/// <summary>
/// Returns visit analytics for a brand's CMS site over a given number of days.
/// Includes total visits, top pages, and daily visit counts.
/// </summary>
public record GetSiteAnalyticsQuery(
    Guid BrandId,
    int Days = 30) : IRequest<SiteAnalyticsDto?>;

public class GetSiteAnalyticsHandler : IRequestHandler<GetSiteAnalyticsQuery, SiteAnalyticsDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetSiteAnalyticsHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SiteAnalyticsDto?> Handle(
        GetSiteAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var brand = await _db.Brands
            .FirstOrDefaultAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId,
                cancellationToken);

        if (brand is null)
            return null;

        var days = Math.Clamp(request.Days, 1, 365);
        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var visits = await _db.SiteVisits
            .Where(v => v.BrandId == request.BrandId && v.VisitedAt >= since)
            .ToListAsync(cancellationToken);

        var topPages = visits
            .GroupBy(v => v.Slug)
            .Select(g => new PageVisitSummary { Slug = g.Key, Visits = g.Count() })
            .OrderByDescending(p => p.Visits)
            .Take(10)
            .ToList();

        var dailyVisits = visits
            .GroupBy(v => DateOnly.FromDateTime(v.VisitedAt.UtcDateTime))
            .Select(g => new DailyVisitCount { Date = g.Key, Visits = g.Count() })
            .OrderBy(d => d.Date)
            .ToList();

        return new SiteAnalyticsDto
        {
            BrandId = brand.Id,
            BrandName = brand.Name,
            TotalVisits = visits.Count,
            UniquePages = topPages.Count,
            TopPages = topPages,
            DailyVisits = dailyVisits,
        };
    }
}
