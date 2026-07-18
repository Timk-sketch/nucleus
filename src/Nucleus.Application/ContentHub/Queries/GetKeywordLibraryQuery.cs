using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ContentHub.DTOs;

namespace Nucleus.Application.ContentHub.Queries;

/// <summary>
/// Returns the full keyword library for a brand, enriched with:
/// - Latest rank position from keyword_ranks
/// - Count of ContentPages targeting each keyword
/// Tenant-scoped via global EF query filter.
/// </summary>
public record GetKeywordLibraryQuery(
    Guid BrandId,
    string? Search = null,
    int Page = 1,
    int PageSize = 50) : IRequest<KeywordLibraryDto?>;

public class GetKeywordLibraryHandler : IRequestHandler<GetKeywordLibraryQuery, KeywordLibraryDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetKeywordLibraryHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<KeywordLibraryDto?> Handle(
        GetKeywordLibraryQuery request, CancellationToken cancellationToken)
    {
        var brand = await _db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId)
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null) return null;

        var query = _db.BrandKeywords
            .Where(k => k.BrandId == request.BrandId);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(k => k.Keyword.Contains(request.Search.ToLowerInvariant()));

        var total = await query.CountAsync(cancellationToken);

        var keywords = await query
            .OrderBy(k => k.Keyword)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(k => new
            {
                k.Id,
                k.Keyword,
                k.TargetUrl,
                k.Notes,
                k.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        // Enrich with latest rank and content count
        var keywordIds = keywords.Select(k => k.Id).ToList();

        // Latest rank per keyword
        var latestRanks = await _db.KeywordRanks
            .Where(r => keywordIds.Contains(r.KeywordId))
            .GroupBy(r => r.KeywordId)
            .Select(g => new
            {
                KeywordId = g.Key,
                LatestRank = g.OrderByDescending(r => r.CheckedAt).First(),
            })
            .ToListAsync(cancellationToken);

        var rankByKeyword = latestRanks.ToDictionary(
            r => r.KeywordId,
            r => r.LatestRank);

        // Content page count per keyword
        var contentCounts = await _db.ContentPages
            .Where(p => p.BrandId == request.BrandId && p.KeywordId != null && keywordIds.Contains(p.KeywordId.Value))
            .GroupBy(p => p.KeywordId!.Value)
            .Select(g => new { KeywordId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var contentCountByKeyword = contentCounts.ToDictionary(c => c.KeywordId, c => c.Count);

        var items = keywords.Select(k =>
        {
            rankByKeyword.TryGetValue(k.Id, out var rank);
            contentCountByKeyword.TryGetValue(k.Id, out var contentCount);
            return new KeywordItemDto
            {
                Id = k.Id,
                Keyword = k.Keyword,
                TargetUrl = k.TargetUrl,
                Notes = k.Notes,
                LatestPosition = rank?.Position,
                PreviousPosition = rank?.PreviousPosition,
                LastCheckedAt = rank?.CheckedAt,
                CreatedAt = k.CreatedAt,
                ContentCount = contentCount,
            };
        }).ToList();

        return new KeywordLibraryDto
        {
            BrandId = brand.Id,
            BrandName = brand.Name,
            TotalKeywords = total,
            Keywords = items,
        };
    }
}
