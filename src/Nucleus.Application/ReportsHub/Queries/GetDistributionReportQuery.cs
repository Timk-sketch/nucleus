using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ReportsHub.DTOs;

namespace Nucleus.Application.ReportsHub.Queries;

public record GetDistributionReportQuery(Guid BrandId, int Days = 30) : IRequest<DistributionReportDto?>;

public class GetDistributionReportQueryHandler(INucleusDbContext db)
    : IRequestHandler<GetDistributionReportQuery, DistributionReportDto?>
{
    public async Task<DistributionReportDto?> Handle(GetDistributionReportQuery request, CancellationToken ct)
    {
        var days  = Math.Clamp(request.Days, 1, 365);
        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var brandExists = await db.Brands.AnyAsync(b => b.Id == request.BrandId, ct);
        if (!brandExists) return null;

        // Email campaign messages
        var emailStats = await db.EmailCampaignMessages
            .Where(m => m.BrandId == request.BrandId && m.SentAt >= since && m.Status == "sent")
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count      = g.Count(),
                Recipients = g.Sum(m => m.RecipientCount),
                Opens      = g.Sum(m => m.OpenCount),
                Clicks     = g.Sum(m => m.ClickCount),
            })
            .FirstOrDefaultAsync(ct);

        // Social posts by platform
        var byPlatform = await db.SocialPosts
            .Where(p => p.BrandId == request.BrandId && p.CreatedAt >= since)
            .GroupBy(p => p.Platform)
            .Select(g => new SocialPlatformRow
            {
                Platform  = g.Key,
                Published = g.Count(p => p.Status == "published"),
                Scheduled = g.Count(p => p.Status == "scheduled"),
                Failed    = g.Count(p => p.Status == "failed"),
            })
            .ToListAsync(ct);

        // SendLog reach (email channel)
        int emailReach = await db.SendLogs
            .Where(s => s.BrandId == request.BrandId && s.Channel == "email" && s.SentAt >= since)
            .SumAsync(s => s.RecipientCount, ct);

        int socialPublished = byPlatform.Sum(p => p.Published);
        int socialFailed    = byPlatform.Sum(p => p.Failed);

        int opens      = emailStats?.Opens ?? 0;
        int recipients = emailStats?.Recipients ?? 0;
        int clicks     = emailStats?.Clicks ?? 0;

        return new DistributionReportDto
        {
            DaysWindow            = days,
            EmailMessagesSent     = emailStats?.Count ?? 0,
            EmailTotalRecipients  = recipients,
            EmailTotalOpens       = opens,
            EmailTotalClicks      = clicks,
            EmailOpenRate         = recipients > 0 ? (double)opens  / recipients : null,
            EmailClickRate        = opens      > 0 ? (double)clicks / opens      : null,
            SocialPostsPublished  = socialPublished,
            SocialPostsFailed     = socialFailed,
            ByPlatform            = byPlatform,
            TotalEmailReach       = emailReach,
            TotalSocialPosts      = socialPublished,
        };
    }
}
