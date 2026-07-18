using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.AuthorityHub.DTOs;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.AuthorityHub.Queries;

/// <summary>
/// Returns brand mentions for a brand, optionally filtered by review status and sentiment.
/// Ordered newest-first. Tenant-scoped.
/// </summary>
public record GetBrandMentionsQuery(
    Guid BrandId,
    bool UnreviewedOnly = false,
    string? Sentiment = null,
    int Page = 1,
    int PageSize = 50) : IRequest<List<BrandMentionDto>>;

public class GetBrandMentionsHandler : IRequestHandler<GetBrandMentionsQuery, List<BrandMentionDto>>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetBrandMentionsHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<BrandMentionDto>> Handle(
        GetBrandMentionsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.BrandMentions
            .Where(m => m.BrandId == request.BrandId);

        if (request.UnreviewedOnly)
            query = query.Where(m => !m.IsReviewed);

        if (!string.IsNullOrWhiteSpace(request.Sentiment))
            query = query.Where(m => m.Sentiment == request.Sentiment.ToLowerInvariant());

        return await query
            .OrderByDescending(m => m.DiscoveredAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new BrandMentionDto
            {
                Id = m.Id,
                BrandId = m.BrandId,
                SourceUrl = m.SourceUrl,
                MentionText = m.MentionText,
                Sentiment = m.Sentiment,
                DiscoveredAt = m.DiscoveredAt,
                IsReviewed = m.IsReviewed,
            })
            .ToListAsync(cancellationToken);
    }
}
