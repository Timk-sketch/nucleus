using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.Search.DTOs;

namespace Nucleus.Application.Search.Queries;

/// <summary>
/// Returns all search alerts for a brand. Optionally filter to active-only.
/// </summary>
public record GetSearchAlertsQuery(Guid BrandId, bool ActiveOnly = false) : IRequest<List<SearchAlertDto>>;

public class GetSearchAlertsHandler : IRequestHandler<GetSearchAlertsQuery, List<SearchAlertDto>>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetSearchAlertsHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<SearchAlertDto>> Handle(
        GetSearchAlertsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.SearchAlerts
            .Where(a => a.BrandId == request.BrandId && a.TenantId == _tenant.TenantId);

        if (request.ActiveOnly)
            query = query.Where(a => a.IsActive);

        return await query
            .OrderByDescending(a => a.TriggeredAt ?? a.CreatedAt)
            .Select(a => new SearchAlertDto
            {
                Id = a.Id,
                KeywordId = a.KeywordId,
                Keyword = a.Keyword.Keyword,
                AlertType = a.AlertType,
                Threshold = a.Threshold,
                IsActive = a.IsActive,
                Message = a.Message,
                TriggeredAt = a.TriggeredAt,
                CreatedAt = a.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }
}
