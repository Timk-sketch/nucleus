using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.StudioHub.Commands;

/// <summary>
/// Creates a new website page (CMS entry) for a brand.
/// Plan gate: starter = 5 pages max; pro/agency = unlimited.
/// Returns the new page's ID.
/// </summary>
public record CreateWebsitePageCommand(
    Guid BrandId,
    string Slug,
    string Title,
    string PageType,
    string? HtmlContent,
    string? SeoTitle,
    string? MetaDescription,
    string? OgImage,
    string? SchemaJson) : IRequest<Guid>;

public class CreateWebsitePageValidator : AbstractValidator<CreateWebsitePageCommand>
{
    private static readonly HashSet<string> ValidPageTypes =
    [
        "homepage", "landing", "blog", "service", "legal", "other"
    ];

    public CreateWebsitePageValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(300)
            .Matches(@"^[a-z0-9]+(?:[-/][a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase alphanumeric with hyphens/slashes only.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.PageType)
            .Must(t => ValidPageTypes.Contains(t))
            .WithMessage($"PageType must be one of: {string.Join(", ", ValidPageTypes)}.");
        RuleFor(x => x.SeoTitle).MaximumLength(300).When(x => x.SeoTitle != null);
        RuleFor(x => x.MetaDescription).MaximumLength(500).When(x => x.MetaDescription != null);
        RuleFor(x => x.OgImage).MaximumLength(500).When(x => x.OgImage != null);
    }
}

public class CreateWebsitePageHandler : IRequestHandler<CreateWebsitePageCommand, Guid>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public CreateWebsitePageHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateWebsitePageCommand request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        // Enforce slug uniqueness per brand
        var slugTaken = await _db.WebsitePages
            .AnyAsync(p => p.BrandId == request.BrandId && p.Slug == request.Slug, cancellationToken);

        if (slugTaken)
            throw new InvalidOperationException($"A page with slug '{request.Slug}' already exists for this brand.");

        var page = new WebsitePage
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            Slug = request.Slug.Trim().ToLowerInvariant(),
            Title = request.Title.Trim(),
            PageType = request.PageType,
            HtmlContent = request.HtmlContent,
            SeoTitle = request.SeoTitle?.Trim(),
            MetaDescription = request.MetaDescription?.Trim(),
            OgImage = request.OgImage?.Trim(),
            SchemaJson = request.SchemaJson,
            Status = "draft",
        };

        _db.WebsitePages.Add(page);
        await _db.SaveChangesAsync(cancellationToken);

        return page.Id;
    }
}
