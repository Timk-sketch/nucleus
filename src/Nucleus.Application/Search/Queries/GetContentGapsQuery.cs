using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.Search.DTOs;

namespace Nucleus.Application.Search.Queries;

/// <summary>
/// Content gaps = keywords the brand is ranking for but has no dedicated page/content for.
/// "No dedicated content" = BrandKeyword.TargetUrl is null or empty.
/// Ranked = has at least one KeywordRank in the last 30 days.
/// </summary>
public record GetContentGapsQuery(Guid BrandId) : IRequest<List<ContentGapDto>>;

public class GetContentGapsHandler : IRequestHandler<GetContentGapsQuery, List<ContentGapDto>>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetContentGapsHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<ContentGapDto>> Handle(
        GetContentGapsQuery request, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);

        var keywords = await _db.BrandKeywords
            .Where(k => k.BrandId == request.BrandId && k.TenantId == _tenant.TenantId)
            .Select(k => new { k.Id, k.Keyword, k.TargetUrl })
            .ToListAsync(cancellationToken);

        if (keywords.Count == 0) return [];

        var keywordIds = keywords.Select(k => k.Id).ToList();

        // Latest rank per keyword in the last 30 days
        var latestRanks = await _db.KeywordRanks
            .Where(r => keywordIds.Contains(r.KeywordId) && r.CheckedAt >= cutoff)
            .GroupBy(r => r.KeywordId)
            .Select(g => g.OrderByDescending(r => r.CheckedAt).First())
            .ToListAsync(cancellationToken);

        var rankLookup = latestRanks.ToDictionary(r => r.KeywordId);

        // Content gap = ranking (position ≤ 100) but no TargetUrl set
        var gaps = keywords
            .Where(kw =>
            {
                rankLookup.TryGetValue(kw.Id, out var rank);
                return rank?.Position is not null && rank.Position <= 100;
            })
            .Select(kw =>
            {
                rankLookup.TryGetValue(kw.Id, out var rank);
                return new ContentGapDto
                {
                    KeywordId = kw.Id,
                    Keyword = kw.Keyword,
                    CurrentPosition = rank?.Position,
                    SearchVolume = rank?.SearchVolume,
                    RankedUrl = rank?.RankedUrl,
                    HasDedicatedContent = !string.IsNullOrEmpty(kw.TargetUrl),
                    TargetUrl = kw.TargetUrl,
                };
            })
            .OrderBy(g => g.CurrentPosition ?? 999)
            .ToList();

        return gaps;
    }
}
