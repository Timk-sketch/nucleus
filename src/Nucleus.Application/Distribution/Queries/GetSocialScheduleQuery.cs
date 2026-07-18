using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.Distribution.DTOs;

namespace Nucleus.Application.Distribution.Queries;

/// <summary>
/// Returns all social posts for a brand within a date window.
/// Defaults to the next 30 days if no window is specified.
/// </summary>
public record GetSocialScheduleQuery(
    Guid BrandId,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? StatusFilter = null) : IRequest<List<SocialPostDto>>;

public class GetSocialScheduleHandler : IRequestHandler<GetSocialScheduleQuery, List<SocialPostDto>>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetSocialScheduleHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<SocialPostDto>> Handle(
        GetSocialScheduleQuery request, CancellationToken cancellationToken)
    {
        var from = request.From ?? DateTimeOffset.UtcNow.AddDays(-7);
        var to = request.To ?? DateTimeOffset.UtcNow.AddDays(30);

        var query = _db.SocialPosts
            .Where(p => p.BrandId == request.BrandId
                     && p.TenantId == _tenant.TenantId
                     && p.ScheduledAt >= from
                     && p.ScheduledAt <= to);

        if (!string.IsNullOrWhiteSpace(request.StatusFilter))
            query = query.Where(p => p.Status == request.StatusFilter);

        return await query
            .OrderBy(p => p.ScheduledAt)
            .Select(p => new SocialPostDto
            {
                Id = p.Id,
                BrandId = p.BrandId,
                BrandName = p.Brand.Name,
                ContentPageId = p.ContentPageId,
                Platform = p.Platform,
                Caption = p.Caption,
                ImageUrl = p.ImageUrl,
                ScheduledAt = p.ScheduledAt,
                PublishedAt = p.PublishedAt,
                Status = p.Status,
                ExternalPostId = p.ExternalPostId,
                ErrorMessage = p.ErrorMessage,
                Provider = p.Provider,
                CreatedAt = p.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }
}
