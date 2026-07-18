using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.AuthorityHub.DTOs;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.AuthorityHub.Queries;

/// <summary>
/// Returns the full backlink profile for a brand: summary stats + individual backlink rows.
/// Tenant-scoped via global EF query filter + explicit TenantId check on the brand.
/// Plan gate: backlink_tracking = pro+
/// </summary>
public record GetBacklinkProfileQuery(
    Guid BrandId,
    bool ActiveOnly = false,
    int Page = 1,
    int PageSize = 50) : IRequest<BacklinkProfileDto?>;

public class GetBacklinkProfileHandler : IRequestHandler<GetBacklinkProfileQuery, BacklinkProfileDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetBacklinkProfileHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<BacklinkProfileDto?> Handle(
        GetBacklinkProfileQuery request, CancellationToken cancellationToken)
    {
        var brand = await _db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId)
            .Select(b => new { b.Id, b.Name, b.Domain })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null) return null;

        var cutoff30 = DateTimeOffset.UtcNow.AddDays(-30);

        // Aggregate stats (over ALL backlinks for this brand, ignoring pagination)
        var allStats = await _db.BacklinkRecords
            .Where(b => b.BrandId == request.BrandId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(b => b.IsActive),
                Lost = g.Count(b => !b.IsActive),
                NewLast30 = g.Count(b => b.FirstSeenAt >= cutoff30),
                AvgDr = g.Average(b => (decimal?)b.DomainRating),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Paginated rows
        var query = _db.BacklinkRecords
            .Where(b => b.BrandId == request.BrandId);

        if (request.ActiveOnly)
            query = query.Where(b => b.IsActive);

        var rows = await query
            .OrderByDescending(b => b.DomainRating)
            .ThenByDescending(b => b.FirstSeenAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(b => new BacklinkRecordDto
            {
                Id = b.Id,
                BrandId = b.BrandId,
                SourceUrl = b.SourceUrl,
                TargetUrl = b.TargetUrl,
                AnchorText = b.AnchorText,
                DomainRating = b.DomainRating,
                FirstSeenAt = b.FirstSeenAt,
                LastSeenAt = b.LastSeenAt,
                IsActive = b.IsActive,
            })
            .ToListAsync(cancellationToken);

        return new BacklinkProfileDto
        {
            BrandId = brand.Id,
            BrandName = brand.Name,
            Domain = brand.Domain,
            TotalBacklinks = allStats?.Total ?? 0,
            ActiveBacklinks = allStats?.Active ?? 0,
            LostBacklinks = allStats?.Lost ?? 0,
            NewLast30Days = allStats?.NewLast30 ?? 0,
            AverageDomainRating = allStats?.AvgDr,
            Backlinks = rows,
        };
    }
}
