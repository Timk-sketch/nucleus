using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ContentHub.DTOs;

namespace Nucleus.Application.ContentHub.Queries;

/// <summary>
/// Returns all ContentPages currently in "review" status for the given brand.
/// These are pages submitted for editorial approval.
/// Also returns recently approved/rejected pages (last 30 days) for context.
/// Tenant-scoped.
/// </summary>
public record GetContentApprovalQueueQuery(
    Guid BrandId,
    bool IncludeRecent = true) : IRequest<ApprovalQueueResult?>;

public record ApprovalQueueResult(
    List<ContentPageDto> PendingReview,
    List<ContentPageDto> RecentlyReviewed);

public class GetContentApprovalQueueHandler : IRequestHandler<GetContentApprovalQueueQuery, ApprovalQueueResult?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetContentApprovalQueueHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<ApprovalQueueResult?> Handle(
        GetContentApprovalQueueQuery request, CancellationToken cancellationToken)
    {
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists) return null;

        var pending = await _db.ContentPages
            .Where(p => p.BrandId == request.BrandId && p.Status == "review")
            .OrderBy(p => p.CreatedAt)
            .Select(p => new ContentPageDto
            {
                Id = p.Id,
                BrandId = p.BrandId,
                KeywordId = p.KeywordId,
                KeywordText = p.Keyword != null ? p.Keyword.Keyword : null,
                Title = p.Title,
                PageType = p.PageType,
                Status = p.Status,
                WordCount = p.WordCount,
                AiModel = p.AiModel,
                ReviewNotes = p.ReviewNotes,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        var recentlyReviewed = new List<ContentPageDto>();

        if (request.IncludeRecent)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
            recentlyReviewed = await _db.ContentPages
                .Where(p => p.BrandId == request.BrandId
                         && (p.Status == "approved" || p.Status == "draft")
                         && p.ReviewNotes != null
                         && p.UpdatedAt >= cutoff)
                .OrderByDescending(p => p.UpdatedAt)
                .Take(20)
                .Select(p => new ContentPageDto
                {
                    Id = p.Id,
                    BrandId = p.BrandId,
                    KeywordId = p.KeywordId,
                    KeywordText = p.Keyword != null ? p.Keyword.Keyword : null,
                    Title = p.Title,
                    PageType = p.PageType,
                    Status = p.Status,
                    WordCount = p.WordCount,
                    ReviewNotes = p.ReviewNotes,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                })
                .ToListAsync(cancellationToken);
        }

        return new ApprovalQueueResult(pending, recentlyReviewed);
    }
}
