using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.FinderHub.DTOs;

namespace Nucleus.Application.FinderHub.Queries;

/// <summary>
/// Returns aggregate analytics for a Finder over the specified day window.
/// Days parameter is clamped to 1-365.
/// Returns null if the finder is not found for this tenant.
/// </summary>
public record GetFinderAnalyticsQuery(
    Guid FinderId,
    int Days = 30) : IRequest<FinderAnalyticsDto?>;

public class GetFinderAnalyticsHandler : IRequestHandler<GetFinderAnalyticsQuery, FinderAnalyticsDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetFinderAnalyticsHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<FinderAnalyticsDto?> Handle(
        GetFinderAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var finder = await _db.Finders
            .FirstOrDefaultAsync(
                f => f.Id == request.FinderId && f.TenantId == _tenant.TenantId,
                cancellationToken);

        if (finder is null)
            return null;

        var days = Math.Clamp(request.Days, 1, 365);
        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days + 1));

        var rows = await _db.FinderAnalytics
            .Where(a => a.FinderId == finder.Id && a.Date >= fromDate)
            .OrderBy(a => a.Date)
            .ToListAsync(cancellationToken);

        var totalStarts = rows.Sum(r => r.Starts);
        var totalCompletions = rows.Sum(r => r.Completions);
        var totalConversions = rows.Sum(r => r.Conversions);

        return new FinderAnalyticsDto
        {
            FinderId = finder.Id,
            FinderName = finder.Name,
            TotalStarts = totalStarts,
            TotalCompletions = totalCompletions,
            TotalConversions = totalConversions,
            CompletionRate = totalStarts > 0
                ? Math.Round((double)totalCompletions / totalStarts, 4)
                : null,
            ConversionRate = totalCompletions > 0
                ? Math.Round((double)totalConversions / totalCompletions, 4)
                : null,
            DailyStats = rows.Select(r => new DailyFinderStats
            {
                Date = r.Date,
                Starts = r.Starts,
                Completions = r.Completions,
                Conversions = r.Conversions,
            }).ToList(),
        };
    }
}
