using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.Search.DTOs;

namespace Nucleus.Application.Search.Queries;

/// <summary>
/// Returns page performance metrics: groups keywords by their ranked URL to show
/// which pages are driving the most search visibility.
/// </summary>
public record GetPagePerformanceQuery(Guid BrandId) : IRequest<List<PagePerformanceDto>>;

public class GetPagePerformanceHandler : IRequestHandler<GetPagePerformanceQuery, List<PagePerformanceDto>>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetPagePerformanceHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<PagePerformanceDto>> Handle(
        GetPagePerformanceQuery request, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);

        var keywords = await _db.BrandKeywords
            .Where(k => k.BrandId == request.BrandId && k.TenantId == _tenant.TenantId)
            .Select(k => new { k.Id, k.Keyword })
            .ToListAsync(cancellationToken);

        if (keywords.Count == 0) return [];

        var keywordIds = keywords.Select(k => k.Id).ToList();
        var keywordLookup = keywords.ToDictionary(k => k.Id, k => k.Keyword);

        // Get latest rank per keyword
        var latestRanks = await _db.KeywordRanks
            .Where(r => keywordIds.Contains(r.KeywordId)
                     && r.CheckedAt >= cutoff
                     && r.Position.HasValue
                     && r.RankedUrl != null)
            .GroupBy(r => r.KeywordId)
            .Select(g => g.OrderByDescending(r => r.CheckedAt).First())
            .ToListAsync(cancellationToken);

        if (latestRanks.Count == 0) return [];

        // Group by ranked URL
        var byPage = latestRanks
            .Where(r => !string.IsNullOrEmpty(r.RankedUrl))
            .GroupBy(r => r.RankedUrl!)
            .Select(g =>
            {
                var positions = g.Select(r => r.Position!.Value).ToList();
                var topKeywords = g
                    .OrderBy(r => r.Position)
                    .Take(5)
                    .Select(r => keywordLookup.GetValueOrDefault(r.KeywordId, ""))
                    .Where(kw => !string.IsNullOrEmpty(kw))
                    .ToList();

                return new PagePerformanceDto
                {
                    Url = g.Key,
                    KeywordCount = g.Count(),
                    AveragePosition = Math.Round(positions.Average(), 1),
                    BestPosition = positions.Min(),
                    TotalSearchVolume = g.Sum(r => r.SearchVolume),
                    TopKeywords = topKeywords,
                };
            })
            .OrderBy(p => p.BestPosition)
            .ToList();

        return byPage;
    }
}
