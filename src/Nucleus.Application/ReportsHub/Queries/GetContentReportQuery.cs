using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ReportsHub.DTOs;

namespace Nucleus.Application.ReportsHub.Queries;

public record GetContentReportQuery(Guid BrandId, int Days = 30) : IRequest<ContentReportDto?>;

public class GetContentReportQueryHandler(INucleusDbContext db)
    : IRequestHandler<GetContentReportQuery, ContentReportDto?>
{
    public async Task<ContentReportDto?> Handle(GetContentReportQuery request, CancellationToken ct)
    {
        var days = Math.Clamp(request.Days, 1, 365);
        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var brandExists = await db.Brands.AnyAsync(b => b.Id == request.BrandId, ct);
        if (!brandExists) return null;

        // Status breakdown (all-time)
        var byStatus = await db.ContentPages
            .Where(p => p.BrandId == request.BrandId)
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Page type breakdown
        var byType = await db.ContentPages
            .Where(p => p.BrandId == request.BrandId)
            .GroupBy(p => p.PageType)
            .Select(g => new
            {
                PageType  = g.Key,
                Count     = g.Count(),
                Published = g.Count(p => p.Status == "published"),
            })
            .ToListAsync(ct);

        // AI cost by feature (last N days)
        var aiByFeature = await db.AiUsages
            .Where(u => u.BrandId == request.BrandId && u.CreatedAt >= since)
            .GroupBy(u => new { u.Feature, u.Model })
            .Select(g => new AiFeatureRow
            {
                Feature = g.Key.Feature,
                Model   = g.Key.Model ?? "",
                Tokens  = g.Sum(u => u.TokensUsed),
                CostUsd = g.Sum(u => u.CostUsd),
            })
            .OrderByDescending(r => r.CostUsd)
            .ToListAsync(ct);

        int statusCount(string s) => byStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        int total = byStatus.Sum(x => x.Count);

        return new ContentReportDto
        {
            DaysWindow     = days,
            TotalPages     = total,
            Draft          = statusCount("draft"),
            InReview       = statusCount("review"),
            Approved       = statusCount("approved"),
            Published      = statusCount("published"),
            Rejected       = statusCount("rejected"),
            ByPageType     = byType.Select(t => new ContentTypeRow
            {
                PageType  = t.PageType,
                Count     = t.Count,
                Published = t.Published,
            }).ToList(),
            TotalAiSpendUsd = aiByFeature.Sum(r => r.CostUsd),
            TotalAiTokens   = aiByFeature.Sum(r => r.Tokens),
            AiByFeature     = aiByFeature,
        };
    }
}
