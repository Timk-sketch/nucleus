using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.StudioHub.DTOs;

namespace Nucleus.Application.StudioHub.Queries;

/// <summary>
/// Returns the page library for a brand: summary stats + paginated pages.
/// Filterable by status (draft | published | archived) and page_type.
/// Tenant-scoped via global EF query filter.
/// </summary>
public record GetPageLibraryQuery(
    Guid BrandId,
    string? Status = null,
    string? PageType = null,
    int Page = 1,
    int PageSize = 50) : IRequest<PageLibraryDto?>;

public class GetPageLibraryHandler : IRequestHandler<GetPageLibraryQuery, PageLibraryDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetPageLibraryHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<PageLibraryDto?> Handle(GetPageLibraryQuery request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brand = await _db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId)
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null) return null;

        // Aggregate stats (all statuses)
        var stats = await _db.WebsitePages
            .Where(p => p.BrandId == request.BrandId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Published = g.Count(p => p.Status == "published"),
                Draft = g.Count(p => p.Status == "draft"),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Paginated rows with optional filters
        var query = _db.WebsitePages
            .Where(p => p.BrandId == request.BrandId);

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(p => p.Status == request.Status);

        if (!string.IsNullOrWhiteSpace(request.PageType))
            query = query.Where(p => p.PageType == request.PageType);

        var pages = await query
            .OrderByDescending(p => p.UpdatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new WebsitePageDto
            {
                Id = p.Id,
                BrandId = p.BrandId,
                Slug = p.Slug,
                Title = p.Title,
                PageType = p.PageType,
                SeoTitle = p.SeoTitle,
                MetaDescription = p.MetaDescription,
                OgImage = p.OgImage,
                Status = p.Status,
                PublishedAt = p.PublishedAt,
                SchemaJson = p.SchemaJson,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                // HtmlContent omitted from list view for performance
            })
            .ToListAsync(cancellationToken);

        return new PageLibraryDto
        {
            BrandId = brand.Id,
            BrandName = brand.Name,
            TotalPages = stats?.Total ?? 0,
            PublishedPages = stats?.Published ?? 0,
            DraftPages = stats?.Draft ?? 0,
            Pages = pages,
        };
    }
}
