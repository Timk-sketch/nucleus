using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.StudioHub.DTOs;

namespace Nucleus.Application.StudioHub.Commands;

/// <summary>
/// Updates an existing WebsitePage's content and metadata fields.
/// Slug is intentionally excluded — it is immutable after creation.
/// Returns the updated WebsitePageDto, or null if the page was not found.
/// </summary>
public record UpdateWebsitePageCommand(
    Guid PageId,
    string Title,
    string PageType,
    string? HtmlContent,
    string? SeoTitle,
    string? MetaDescription,
    string? OgImage,
    string? SchemaJson) : IRequest<WebsitePageDto?>;

public class UpdateWebsitePageValidator : AbstractValidator<UpdateWebsitePageCommand>
{
    public UpdateWebsitePageValidator()
    {
        RuleFor(x => x.PageId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PageType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SeoTitle).MaximumLength(120).When(x => x.SeoTitle != null);
        RuleFor(x => x.MetaDescription).MaximumLength(300).When(x => x.MetaDescription != null);
    }
}

public class UpdateWebsitePageHandler(INucleusDbContext db, ICurrentTenantService tenant)
    : IRequestHandler<UpdateWebsitePageCommand, WebsitePageDto?>
{
    public async Task<WebsitePageDto?> Handle(UpdateWebsitePageCommand request, CancellationToken cancellationToken)
    {
        var page = await db.WebsitePages
            .FirstOrDefaultAsync(
                p => p.Id == request.PageId && p.TenantId == tenant.TenantId,
                cancellationToken);

        if (page is null) return null;

        page.Title = request.Title;
        page.PageType = request.PageType;
        page.HtmlContent = request.HtmlContent;
        page.SeoTitle = request.SeoTitle;
        page.MetaDescription = request.MetaDescription;
        page.OgImage = request.OgImage;
        page.SchemaJson = request.SchemaJson;

        await db.SaveChangesAsync(cancellationToken);

        return new WebsitePageDto
        {
            Id = page.Id,
            BrandId = page.BrandId,
            Slug = page.Slug,
            Title = page.Title,
            PageType = page.PageType,
            HtmlContent = page.HtmlContent,
            SeoTitle = page.SeoTitle,
            MetaDescription = page.MetaDescription,
            OgImage = page.OgImage,
            SchemaJson = page.SchemaJson,
            Status = page.Status,
            PublishedAt = page.PublishedAt,
            CreatedAt = page.CreatedAt,
            UpdatedAt = page.UpdatedAt,
        };
    }
}
