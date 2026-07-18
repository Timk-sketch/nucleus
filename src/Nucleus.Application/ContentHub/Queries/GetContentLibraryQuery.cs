using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ContentHub.DTOs;

namespace Nucleus.Application.ContentHub.Queries;

/// <summary>
/// Returns the full content library for a brand — all ContentPages, optionally filtered by status,
/// page type, or keyword. Tenant-scoped. Includes keyword text for display.
/// </summary>
public record GetContentLibraryQuery(
    Guid BrandId,
    string? Status = null,
    string? PageType = null,
    Guid? KeywordId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20) : IRequest<ContentLibraryResult?>;

public record ContentLibraryResult(
    int Total,
    List<ContentPageDto> Pages);

public class GetContentLibraryHandler : IRequestHandler<GetContentLibraryQuery, ContentLibraryResult?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetContentLibraryHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<ContentLibraryResult?> Handle(
        GetContentLibraryQuery request, CancellationToken cancellationToken)
    {
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists) return null;

        var query = _db.ContentPages
            .Where(p => p.BrandId == request.BrandId);

        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(p => p.Status == request.Status);

        if (!string.IsNullOrWhiteSpace(request.PageType))
            query = query.Where(p => p.PageType == request.PageType);

        if (request.KeywordId.HasValue)
            query = query.Where(p => p.KeywordId == request.KeywordId.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(p => p.Title.Contains(request.Search));

        var total = await query.CountAsync(cancellationToken);

        var pages = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new ContentPageDto
            {
                Id = p.Id,
                BrandId = p.BrandId,
                KeywordId = p.KeywordId,
                KeywordText = p.Keyword != null ? p.Keyword.Keyword : null,
                Title = p.Title,
                PageType = p.PageType,
                Status = p.Status,
                HtmlContent = p.HtmlContent,
                SeoTitle = p.SeoTitle,
                MetaDescription = p.MetaDescription,
                AiModel = p.AiModel,
                WordCount = p.WordCount,
                ScheduledAt = p.ScheduledAt,
                PublishedAt = p.PublishedAt,
                ReviewNotes = p.ReviewNotes,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return new ContentLibraryResult(total, pages);
    }
}
