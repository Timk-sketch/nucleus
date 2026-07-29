using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.StudioHub.DTOs;

namespace Nucleus.Application.StudioHub.Queries;

/// <summary>
/// Returns a single WebsitePage by id, including HtmlContent, scoped to the current tenant.
/// Returns null if not found or if the page belongs to a different tenant.
/// </summary>
public record GetWebsitePageQuery(Guid PageId) : IRequest<WebsitePageDto?>;

public class GetWebsitePageHandler(INucleusDbContext db, ICurrentTenantService tenant)
    : IRequestHandler<GetWebsitePageQuery, WebsitePageDto?>
{
    public async Task<WebsitePageDto?> Handle(GetWebsitePageQuery request, CancellationToken cancellationToken)
    {
        return await db.WebsitePages
            .Where(p => p.Id == request.PageId && p.TenantId == tenant.TenantId)
            .Select(p => new WebsitePageDto
            {
                Id = p.Id,
                BrandId = p.BrandId,
                Slug = p.Slug,
                Title = p.Title,
                PageType = p.PageType,
                HtmlContent = p.HtmlContent,
                SeoTitle = p.SeoTitle,
                MetaDescription = p.MetaDescription,
                OgImage = p.OgImage,
                SchemaJson = p.SchemaJson,
                Status = p.Status,
                PublishedAt = p.PublishedAt,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
