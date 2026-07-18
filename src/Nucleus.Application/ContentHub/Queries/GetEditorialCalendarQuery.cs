using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ContentHub.DTOs;

namespace Nucleus.Application.ContentHub.Queries;

/// <summary>
/// Returns the editorial calendar for a brand — all ContentPages that have a ScheduledAt date
/// within the specified time window, plus all content published in that window.
/// Ordered by ScheduledAt ascending. Tenant-scoped.
/// </summary>
public record GetEditorialCalendarQuery(
    Guid BrandId,
    DateTimeOffset? WindowStart = null,
    DateTimeOffset? WindowEnd = null) : IRequest<EditorialCalendarDto?>;

public class GetEditorialCalendarHandler : IRequestHandler<GetEditorialCalendarQuery, EditorialCalendarDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetEditorialCalendarHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<EditorialCalendarDto?> Handle(
        GetEditorialCalendarQuery request, CancellationToken cancellationToken)
    {
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists) return null;

        // Default to the current 8-week window
        var start = request.WindowStart ?? DateTimeOffset.UtcNow.AddDays(-7);
        var end = request.WindowEnd ?? DateTimeOffset.UtcNow.AddDays(49); // 7 weeks forward

        var entries = await _db.ContentPages
            .Where(p => p.BrandId == request.BrandId
                && (
                    // Scheduled within window
                    (p.ScheduledAt.HasValue && p.ScheduledAt >= start && p.ScheduledAt <= end)
                    ||
                    // Published within window (even if no schedule date)
                    (p.PublishedAt.HasValue && p.PublishedAt >= start && p.PublishedAt <= end)
                ))
            .OrderBy(p => p.ScheduledAt ?? p.PublishedAt ?? p.CreatedAt)
            .Select(p => new CalendarEntryDto
            {
                Id = p.Id,
                Title = p.Title,
                PageType = p.PageType,
                Status = p.Status,
                KeywordText = p.Keyword != null ? p.Keyword.Keyword : null,
                ScheduledAt = p.ScheduledAt,
                PublishedAt = p.PublishedAt,
                CreatedAt = p.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return new EditorialCalendarDto
        {
            BrandId = request.BrandId,
            WindowStart = start,
            WindowEnd = end,
            Entries = entries,
        };
    }
}
