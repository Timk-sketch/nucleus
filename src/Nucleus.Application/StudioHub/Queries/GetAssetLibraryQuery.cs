using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.StudioHub.DTOs;

namespace Nucleus.Application.StudioHub.Queries;

/// <summary>
/// Returns the asset library for a brand: summary stats + paginated assets.
/// Filterable by asset_type (image | document | font | svg | generated | other).
/// Tenant-scoped via global EF query filter.
/// </summary>
public record GetAssetLibraryQuery(
    Guid BrandId,
    string? AssetType = null,
    int Page = 1,
    int PageSize = 50) : IRequest<AssetLibraryDto?>;

public class GetAssetLibraryHandler : IRequestHandler<GetAssetLibraryQuery, AssetLibraryDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetAssetLibraryHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<AssetLibraryDto?> Handle(GetAssetLibraryQuery request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brand = await _db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId)
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null) return null;

        // Aggregate stats (all asset types)
        var stats = await _db.DesignAssets
            .Where(a => a.BrandId == request.BrandId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Images = g.Count(a => a.AssetType == "image"),
                Generated = g.Count(a => a.AssetType == "generated"),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Paginated rows
        var query = _db.DesignAssets
            .Where(a => a.BrandId == request.BrandId);

        if (!string.IsNullOrWhiteSpace(request.AssetType))
            query = query.Where(a => a.AssetType == request.AssetType);

        var assets = await query
            .OrderByDescending(a => a.UploadedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new DesignAssetDto
            {
                Id = a.Id,
                BrandId = a.BrandId,
                Name = a.Name,
                AssetType = a.AssetType,
                Url = a.Url,
                Width = a.Width,
                Height = a.Height,
                FileSize = a.FileSize,
                UploadedAt = a.UploadedAt,
                PromptUsed = a.PromptUsed,
                MimeType = a.MimeType,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return new AssetLibraryDto
        {
            BrandId = brand.Id,
            BrandName = brand.Name,
            TotalAssets = stats?.Total ?? 0,
            ImageCount = stats?.Images ?? 0,
            GeneratedCount = stats?.Generated ?? 0,
            Assets = assets,
        };
    }
}
