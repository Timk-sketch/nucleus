using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ContentHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.ContentHub.Commands;

/// <summary>
/// Creates a manually authored content page (not AI-generated).
/// Validates brand ownership. Returns the new ContentPage Id.
/// </summary>
public record CreateContentPageCommand(
    Guid BrandId,
    string Title,
    string PageType,
    Guid? KeywordId,
    string? HtmlContent,
    string? SeoTitle,
    string? MetaDescription,
    DateTimeOffset? ScheduledAt) : IRequest<Guid>;

public class CreateContentPageValidator : AbstractValidator<CreateContentPageCommand>
{
    public CreateContentPageValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PageType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.SeoTitle).MaximumLength(300).When(x => x.SeoTitle != null);
        RuleFor(x => x.MetaDescription).MaximumLength(500).When(x => x.MetaDescription != null);
    }
}

public class CreateContentPageHandler : IRequestHandler<CreateContentPageCommand, Guid>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public CreateContentPageHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(
        CreateContentPageCommand request, CancellationToken cancellationToken)
    {
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        // Validate keyword belongs to brand if provided
        if (request.KeywordId.HasValue)
        {
            var kwExists = await _db.BrandKeywords
                .AnyAsync(k => k.Id == request.KeywordId.Value && k.BrandId == request.BrandId, cancellationToken);
            if (!kwExists)
                throw new InvalidOperationException("Keyword not found for this brand.");
        }

        var page = new ContentPage
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            KeywordId = request.KeywordId,
            Title = request.Title,
            PageType = request.PageType,
            Status = "draft",
            HtmlContent = request.HtmlContent,
            SeoTitle = request.SeoTitle,
            MetaDescription = request.MetaDescription,
            ScheduledAt = request.ScheduledAt,
        };

        _db.ContentPages.Add(page);
        await _db.SaveChangesAsync(cancellationToken);

        return page.Id;
    }
}
