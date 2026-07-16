using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.Search.DTOs;

namespace Nucleus.Application.Search.Queries;

/// <summary>
/// Returns 90-day rank history for a specific keyword.
/// Uses KeywordRank as the history table (stored per check).
/// </summary>
public record GetRankHistoryQuery(Guid KeywordId, int DaysBack = 90) : IRequest<RankHistoryDto?>;

public class GetRankHistoryHandler : IRequestHandler<GetRankHistoryQuery, RankHistoryDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetRankHistoryHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<RankHistoryDto?> Handle(
        GetRankHistoryQuery request, CancellationToken cancellationToken)
    {
        var keyword = await _db.BrandKeywords
            .Where(k => k.Id == request.KeywordId && k.TenantId == _tenant.TenantId)
            .Select(k => new { k.Id, k.Keyword, k.TargetUrl })
            .FirstOrDefaultAsync(cancellationToken);

        if (keyword is null) return null;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Abs(request.DaysBack));

        var history = await _db.KeywordRanks
            .Where(r => r.KeywordId == request.KeywordId && r.CheckedAt >= cutoff)
            .OrderBy(r => r.CheckedAt)
            .Select(r => new RankHistoryPointDto
            {
                CheckedAt = r.CheckedAt,
                Position = r.Position,
                RankedUrl = r.RankedUrl,
            })
            .ToListAsync(cancellationToken);

        return new RankHistoryDto
        {
            KeywordId = keyword.Id,
            Keyword = keyword.Keyword,
            TargetUrl = keyword.TargetUrl,
            History = history,
        };
    }
}
