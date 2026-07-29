using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ReportsHub.DTOs;

namespace Nucleus.Application.ReportsHub.Queries;

public record GetSearchReportQuery(Guid BrandId) : IRequest<SearchReportDto?>;

public class GetSearchReportQueryHandler(INucleusDbContext db)
    : IRequestHandler<GetSearchReportQuery, SearchReportDto?>
{
    public async Task<SearchReportDto?> Handle(GetSearchReportQuery request, CancellationToken ct)
    {
        var brandExists = await db.Brands.AnyAsync(b => b.Id == request.BrandId, ct);
        if (!brandExists) return null;

        // Current positions
        var ranks = await db.KeywordRanks
            .Where(r => r.BrandId == request.BrandId)
            .Join(db.BrandKeywords,
                  r => r.KeywordId,
                  k => k.Id,
                  (r, k) => new
                  {
                      Keyword          = k.Keyword,
                      CurrentPosition  = r.Position,
                      PreviousPosition = r.PreviousPosition,
                      SearchVolume     = r.SearchVolume,
                  })
            .ToListAsync(ct);

        int top10    = ranks.Count(r => r.CurrentPosition is >= 1 and <= 10);
        int top30    = ranks.Count(r => r.CurrentPosition is >= 1 and <= 30);
        int top100   = ranks.Count(r => r.CurrentPosition is >= 1 and <= 100);
        int unranked = ranks.Count(r => r.CurrentPosition is null or > 100);
        int totalVol = ranks.Sum(r => r.SearchVolume ?? 0);

        // Movers: keywords where both positions are known and there's a positive delta
        var movers = ranks
            .Where(r => r.CurrentPosition.HasValue && r.PreviousPosition.HasValue)
            .Select(r => new KeywordMoverRow
            {
                Keyword          = r.Keyword,
                CurrentPosition  = r.CurrentPosition,
                PreviousPosition = r.PreviousPosition,
                Delta            = (r.PreviousPosition ?? 0) - (r.CurrentPosition ?? 0), // positive = improved
                SearchVolume     = r.SearchVolume,
            })
            .ToList();

        var topMovers    = movers.Where(m => m.Delta > 0).OrderByDescending(m => m.Delta).Take(10).ToList();
        var topDecliners = movers.Where(m => m.Delta < 0).OrderBy(m => m.Delta).Take(10).ToList();

        return new SearchReportDto
        {
            KeywordsTotal   = ranks.Count,
            Top10           = top10,
            Top30           = top30,
            Top100          = top100,
            Unranked        = unranked,
            TotalSearchVolume = totalVol,
            TopMovers       = topMovers,
            TopDecliners    = topDecliners,
        };
    }
}
