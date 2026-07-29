using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ReportsHub.DTOs;

namespace Nucleus.Application.ReportsHub.Queries;

public record GetBrandOverviewQuery(Guid BrandId, int Days = 30) : IRequest<BrandOverviewDto?>;

public class GetBrandOverviewQueryHandler(INucleusDbContext db)
    : IRequestHandler<GetBrandOverviewQuery, BrandOverviewDto?>
{
    public async Task<BrandOverviewDto?> Handle(GetBrandOverviewQuery request, CancellationToken ct)
    {
        var days = Math.Clamp(request.Days, 1, 365);
        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var brand = await db.Brands
            .Where(b => b.Id == request.BrandId)
            .Select(b => new { b.Id, b.Name, b.TenantId })
            .FirstOrDefaultAsync(ct);

        if (brand is null) return null;

        // Content counts
        var contentCounts = await db.ContentPages
            .Where(p => p.BrandId == request.BrandId)
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int published  = contentCounts.FirstOrDefault(x => x.Status == "published")?.Count ?? 0;
        int inReview   = contentCounts.FirstOrDefault(x => x.Status == "review")?.Count ?? 0;
        int draft      = contentCounts.FirstOrDefault(x => x.Status == "draft")?.Count ?? 0;

        // SEO — current keyword positions
        var ranks = await db.KeywordRanks
            .Where(r => r.BrandId == request.BrandId)
            .Select(r => r.Position)
            .ToListAsync(ct);

        int top10  = ranks.Count(p => p is >= 1 and <= 10);
        int top30  = ranks.Count(p => p is >= 1 and <= 30);
        int total  = ranks.Count;

        // Finder aggregates (last N days)
        var finderStats = await db.FinderAnalytics
            .Where(a => db.Finders
                .Where(f => f.BrandId == request.BrandId)
                .Select(f => f.Id)
                .Contains(a.FinderId)
                && a.Date >= DateOnly.FromDateTime(since.DateTime))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Starts       = g.Sum(a => a.Starts),
                Completions  = g.Sum(a => a.Completions),
                Conversions  = g.Sum(a => a.Conversions),
            })
            .FirstOrDefaultAsync(ct);

        int fStarts      = finderStats?.Starts ?? 0;
        int fCompletions = finderStats?.Completions ?? 0;
        int fConversions = finderStats?.Conversions ?? 0;

        // AI spend (last N days)
        var aiStats = await db.AiUsages
            .Where(u => u.BrandId == request.BrandId && u.CreatedAt >= since)
            .GroupBy(_ => 1)
            .Select(g => new { Cost = g.Sum(u => u.CostUsd), Tokens = g.Sum(u => u.TokensUsed) })
            .FirstOrDefaultAsync(ct);

        // Distribution (last N days)
        int emailReach = await db.SendLogs
            .Where(s => s.BrandId == request.BrandId && s.Channel == "email" && s.SentAt >= since)
            .SumAsync(s => s.RecipientCount, ct);

        int socialPublished = await db.SocialPosts
            .Where(p => p.BrandId == request.BrandId && p.Status == "published"
                     && p.PublishedAt >= since)
            .CountAsync(ct);

        return new BrandOverviewDto
        {
            BrandId              = brand.Id,
            BrandName            = brand.Name,
            DaysWindow           = days,
            ContentPublished     = published,
            ContentInReview      = inReview,
            ContentDraft         = draft,
            KeywordsTop10        = top10,
            KeywordsTop30        = top30,
            KeywordsTotal        = total,
            FinderStarts         = fStarts,
            FinderCompletions    = fCompletions,
            FinderConversions    = fConversions,
            FinderConversionRate = fStarts > 0 ? (double)fConversions / fStarts : null,
            AiSpendUsd           = aiStats?.Cost ?? 0m,
            AiTokensUsed         = aiStats?.Tokens ?? 0,
            EmailReach           = emailReach,
            SocialPostsPublished = socialPublished,
        };
    }
}
