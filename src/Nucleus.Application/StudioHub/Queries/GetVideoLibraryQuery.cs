using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.StudioHub.DTOs;

namespace Nucleus.Application.StudioHub.Queries;

public record VideoLibraryDto
{
    public Guid BrandId { get; set; }
    public int TotalVideos { get; set; }
    public List<VideoAssetDto> Videos { get; set; } = [];
}

/// <summary>
/// Returns paginated video assets for a brand, scoped to the current tenant.
/// </summary>
public record GetVideoLibraryQuery(
    Guid BrandId,
    int Page = 1,
    int PageSize = 50) : IRequest<VideoLibraryDto?>;

public class GetVideoLibraryHandler(INucleusDbContext db, ICurrentTenantService tenant)
    : IRequestHandler<GetVideoLibraryQuery, VideoLibraryDto?>
{
    public async Task<VideoLibraryDto?> Handle(GetVideoLibraryQuery request, CancellationToken cancellationToken)
    {
        var brand = await db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == tenant.TenantId)
            .Select(b => new { b.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null) return null;

        var total = await db.VideoAssets
            .CountAsync(v => v.BrandId == request.BrandId, cancellationToken);

        var videos = await db.VideoAssets
            .Where(v => v.BrandId == request.BrandId)
            .OrderByDescending(v => v.UploadedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(v => new VideoAssetDto
            {
                Id = v.Id,
                BrandId = v.BrandId,
                Name = v.Name,
                Url = v.Url,
                ThumbnailUrl = v.ThumbnailUrl,
                DurationSeconds = v.DurationSeconds,
                Platform = v.Platform,
                UploadedAt = v.UploadedAt,
                Description = v.Description,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return new VideoLibraryDto
        {
            BrandId = request.BrandId,
            TotalVideos = total,
            Videos = videos,
        };
    }
}
