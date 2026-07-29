using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.FinderHub.DTOs;

namespace Nucleus.Application.FinderHub.Queries;

/// <summary>
/// Returns aggregate analytics for a Finder over the specified day window.
/// Days parameter is clamped to 1-365.
/// Includes per-variant breakdown derived from FinderSessions.
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
        var fromDateOffset = DateTimeOffset.UtcNow.AddDays(-days + 1).Date;

        var rows = await _db.FinderAnalytics
            .Where(a => a.FinderId == finder.Id && a.Date >= fromDate)
            .OrderBy(a => a.Date)
            .ToListAsync(cancellationToken);

        var totalStarts = rows.Sum(r => r.Starts);
        var totalCompletions = rows.Sum(r => r.Completions);
        var totalConversions = rows.Sum(r => r.Conversions);

        // Variant breakdown — derived from sessions in the window
        var variants = await _db.FinderVariants
            .Where(v => v.FinderId == finder.Id)
            .Select(v => new { v.Id, v.Name })
            .ToListAsync(cancellationToken);

        var variantBreakdown = new List<VariantBreakdownDto>();
        if (variants.Count > 0)
        {
            var sessions = await _db.FinderSessions
                .Where(s => s.FinderId == finder.Id && s.CreatedAt >= fromDateOffset)
                .Select(s => new { s.VariantId, s.CompletedAt, s.Converted })
                .ToListAsync(cancellationToken);

            // Group by variant (null = control / no variant), keyed by Guid? string for nullable compat
            var groups = sessions
                .GroupBy(s => s.VariantId?.ToString() ?? "__control__")
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var variant in variants)
            {
                var group = groups.TryGetValue(variant.Id.ToString(), out var g) ? g : [];
                var starts = group.Count;
                var completions = group.Count(s => s.CompletedAt.HasValue);
                var conversions = group.Count(s => s.Converted);

                variantBreakdown.Add(new VariantBreakdownDto
                {
                    VariantId = variant.Id,
                    VariantName = variant.Name,
                    Sessions = starts,
                    Completions = completions,
                    Conversions = conversions,
                    CompletionRate = starts > 0 ? Math.Round((double)completions / starts, 4) : null,
                    ConversionRate = completions > 0 ? Math.Round((double)conversions / completions, 4) : null,
                });
            }

            // Control group (no variant)
            if (groups.TryGetValue("__control__", out var ctrl))
            {
                variantBreakdown.Insert(0, new VariantBreakdownDto
                {
                    VariantId = null,
                    VariantName = "Control (no variant)",
                    Sessions = ctrl.Count,
                    Completions = ctrl.Count(s => s.CompletedAt.HasValue),
                    Conversions = ctrl.Count(s => s.Converted),
                    CompletionRate = ctrl.Count > 0 ? Math.Round((double)ctrl.Count(s => s.CompletedAt.HasValue) / ctrl.Count, 4) : null,
                    ConversionRate = ctrl.Count(s => s.CompletedAt.HasValue) > 0
                        ? Math.Round((double)ctrl.Count(s => s.Converted) / ctrl.Count(s => s.CompletedAt.HasValue), 4)
                        : null,
                });
            }
        }

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
            Variants = variantBreakdown,
        };
    }
}
