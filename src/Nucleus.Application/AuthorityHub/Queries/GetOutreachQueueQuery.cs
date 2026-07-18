using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.AuthorityHub.DTOs;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.AuthorityHub.Queries;

/// <summary>
/// Returns the outreach queue for a brand, optionally filtered by status.
/// Tenant-scoped. Plan gate: outreach_queue = agency+
/// Ordered by status priority (pending first), then CreatedAt descending.
/// </summary>
public record GetOutreachQueueQuery(
    Guid BrandId,
    string? Status = null,
    int Page = 1,
    int PageSize = 50) : IRequest<List<OutreachQueueItemDto>>;

public class GetOutreachQueueHandler : IRequestHandler<GetOutreachQueueQuery, List<OutreachQueueItemDto>>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetOutreachQueueHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<OutreachQueueItemDto>> Handle(
        GetOutreachQueueQuery request, CancellationToken cancellationToken)
    {
        var query = _db.OutreachQueueItems
            .Where(o => o.BrandId == request.BrandId);

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(o => o.Status == request.Status.ToLowerInvariant());

        return await query
            .OrderBy(o => o.Status == "pending" ? 0 :
                          o.Status == "emailed" ? 1 :
                          o.Status == "replied" ? 2 :
                          o.Status == "accepted" ? 3 : 4)
            .ThenByDescending(o => o.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(o => new OutreachQueueItemDto
            {
                Id = o.Id,
                BrandId = o.BrandId,
                TargetUrl = o.TargetUrl,
                ContactEmail = o.ContactEmail,
                Status = o.Status,
                Notes = o.Notes,
                OutreachAt = o.OutreachAt,
                CreatedAt = o.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }
}
