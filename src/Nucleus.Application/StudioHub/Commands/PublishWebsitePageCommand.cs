using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.StudioHub.DTOs;

namespace Nucleus.Application.StudioHub.Commands;

/// <summary>
/// Publishes (or unpublishes) a website page.
/// Sets status = "published" and stamps PublishedAt.
/// Returns the updated page DTO, or null if not found.
/// </summary>
public record PublishWebsitePageCommand(
    Guid PageId,
    bool Publish = true) : IRequest<WebsitePageDto?>;

public class PublishWebsitePageValidator : AbstractValidator<PublishWebsitePageCommand>
{
    public PublishWebsitePageValidator()
    {
        RuleFor(x => x.PageId).NotEmpty();
    }
}

public class PublishWebsitePageHandler : IRequestHandler<PublishWebsitePageCommand, WebsitePageDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public PublishWebsitePageHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<WebsitePageDto?> Handle(
        PublishWebsitePageCommand request, CancellationToken cancellationToken)
    {
        var page = await _db.WebsitePages
            .FirstOrDefaultAsync(p => p.Id == request.PageId && p.TenantId == _tenant.TenantId,
                cancellationToken);

        if (page is null) return null;

        if (request.Publish)
        {
            page.Status = "published";
            page.PublishedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            page.Status = "draft";
            // Keep PublishedAt as historical record
        }

        page.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

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
            Status = page.Status,
            PublishedAt = page.PublishedAt,
            SchemaJson = page.SchemaJson,
            CreatedAt = page.CreatedAt,
            UpdatedAt = page.UpdatedAt,
        };
    }
}
