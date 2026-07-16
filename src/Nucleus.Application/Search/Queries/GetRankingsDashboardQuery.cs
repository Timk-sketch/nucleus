using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.Search.DTOs;

namespace Nucleus.Application.Search.Queries;

/// <summary>
/// Returns the rankings dashboard: all keywords for a brand with their current and previous positions.
/// Filters by TenantId. Starter plan returns up to 10 keywords.
/// </summary>
public record GetRankingsDashboardQuery(Guid BrandId) : IRequest<RankingsDashboardDto?>;

public class GetRankingsDashboardHandler : IRequestHandler<GetRankingsDashboardQuery, RankingsDashboardDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetRankingsDashboardHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<RankingsDashboardDto?> Handle(
        GetRankingsDashboardQuery request, CancellationToken cancellationToken)
    {
        var brand = await _db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId)
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null) return null;

        // Get all keywords for this brand
        var keywords = await _db.BrandKeywords
            .Where(k => k.BrandId == request.BrandId)
            .Select(k => new { k.Id, k.Keyword, k.TargetUrl })
            .ToListAsync(cancellationToken);

        if (keywords.Count == 0)
            return new RankingsDashboardDto
            {
                BrandId = brand.Id,
                BrandName = brand.Name,
                TotalKeywords = 0,
                Keywords = [],
            };

        var keywordIds = keywords.Select(k => k.Id).ToList();

        // Get latest rank snapshot for each keyword (most recent KeywordRank row)
        var latestRanks = await _db.KeywordRanks
            .Where(r => keywordIds.Contains(r.KeywordId))
            .GroupBy(r => r.KeywordId)
            .Select(g => g.OrderByDescending(r => r.CheckedAt).First())
            .ToListAsync(cancellationToken);

        var rankLookup = latestRanks.ToDictionary(r => r.KeywordId);

        var rankingDtos = keywords.Select(kw =>
        {
            rankLookup.TryGetValue(kw.Id, out var rank);
            int? delta = null;
            if (rank?.Position.HasValue == true && rank.PreviousPosition.HasValue)
                delta = rank.PreviousPosition.Value - rank.Position.Value;

            return new RankingDto
            {
                KeywordId = kw.Id,
                Keyword = kw.Keyword,
                TargetUrl = kw.TargetUrl,
                CurrentPosition = rank?.Position,
                PreviousPosition = rank?.PreviousPosition,
                PositionDelta = delta,
                RankedUrl = rank?.RankedUrl,
                SearchVolume = rank?.SearchVolume,
                LastChecked = rank?.CheckedAt,
            };
        }).ToList();

        int rankingCount = rankingDtos.Count(r => r.CurrentPosition.HasValue);

        return new RankingsDashboardDto
        {
            BrandId = brand.Id,
            BrandName = brand.Name,
            TotalKeywords = keywords.Count,
            RankingKeywords = rankingCount,
            Top3Keywords = rankingDtos.Count(r => r.CurrentPosition <= 3),
            Top10Keywords = rankingDtos.Count(r => r.CurrentPosition <= 10),
            Top30Keywords = rankingDtos.Count(r => r.CurrentPosition <= 30),
            Keywords = rankingDtos.OrderBy(r => r.CurrentPosition ?? 999).ToList(),
        };
    }
}
