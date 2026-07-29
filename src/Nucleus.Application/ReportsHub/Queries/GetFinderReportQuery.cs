using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ReportsHub.DTOs;

namespace Nucleus.Application.ReportsHub.Queries;

public record GetFinderReportQuery(Guid BrandId, int Days = 30) : IRequest<FinderReportDto?>;

public class GetFinderReportQueryHandler(INucleusDbContext db)
    : IRequestHandler<GetFinderReportQuery, FinderReportDto?>
{
    public async Task<FinderReportDto?> Handle(GetFinderReportQuery request, CancellationToken ct)
    {
        var days  = Math.Clamp(request.Days, 1, 365);
        var since = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));

        var brandExists = await db.Brands.AnyAsync(b => b.Id == request.BrandId, ct);
        if (!brandExists) return null;

        var finders = await db.Finders
            .Where(f => f.BrandId == request.BrandId)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

        if (finders.Count == 0)
            return new FinderReportDto { DaysWindow = days };

        var finderIds = finders.Select(f => f.Id).ToList();

        // Daily analytics aggregated per finder
        var analytics = await db.FinderAnalytics
            .Where(a => finderIds.Contains(a.FinderId) && a.Date >= since)
            .GroupBy(a => a.FinderId)
            .Select(g => new
            {
                FinderId    = g.Key,
                Starts      = g.Sum(a => a.Starts),
                Completions = g.Sum(a => a.Completions),
                Conversions = g.Sum(a => a.Conversions),
            })
            .ToListAsync(ct);

        // Lead capture from sessions
        var leadCounts = await db.FinderSessions
            .Where(s => finderIds.Contains(s.FinderId)
                     && s.CreatedAt >= DateTimeOffset.UtcNow.AddDays(-days)
                     && s.LeadEmail != null)
            .GroupBy(s => s.FinderId)
            .Select(g => new { FinderId = g.Key, Leads = g.Count() })
            .ToListAsync(ct);

        var rows = finders.Select(f =>
        {
            var a      = analytics.FirstOrDefault(x => x.FinderId == f.Id);
            var leads  = leadCounts.FirstOrDefault(x => x.FinderId == f.Id)?.Leads ?? 0;
            int starts = a?.Starts ?? 0;
            int comps  = a?.Completions ?? 0;
            int convs  = a?.Conversions ?? 0;
            return new FinderSummaryRow
            {
                FinderId        = f.Id,
                FinderName      = f.Name,
                Status          = f.Status,
                Starts          = starts,
                Completions     = comps,
                Conversions     = convs,
                LeadsCaptured   = leads,
                CompletionRate  = starts > 0 ? (double)comps / starts : null,
                ConversionRate  = comps  > 0 ? (double)convs / comps  : null,
            };
        }).ToList();

        return new FinderReportDto { DaysWindow = days, Finders = rows };
    }
}
