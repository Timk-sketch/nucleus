using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ContentHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.ContentHub.Commands;

/// <summary>
/// Creates a reusable content template for AI generation.
/// Templates contain a structured prompt body with optional placeholders:
///   {{keyword}}, {{brand}}, {{service}}, {{location}}
/// IsGlobal = true shares the template across all brands in the tenant.
/// </summary>
public record CreateContentTemplateCommand(
    Guid BrandId,
    string Name,
    string PageType,
    string Body,
    bool IsGlobal,
    bool IsActive) : IRequest<ContentTemplateDto>;

public class CreateContentTemplateValidator : AbstractValidator<CreateContentTemplateCommand>
{
    public CreateContentTemplateValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PageType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Body).NotEmpty();
    }
}

public class CreateContentTemplateHandler : IRequestHandler<CreateContentTemplateCommand, ContentTemplateDto>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public CreateContentTemplateHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<ContentTemplateDto> Handle(
        CreateContentTemplateCommand request, CancellationToken cancellationToken)
    {
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        var template = new ContentTemplate
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            Name = request.Name.Trim(),
            PageType = request.PageType,
            Body = request.Body,
            IsGlobal = request.IsGlobal,
            IsActive = request.IsActive,
        };

        _db.ContentTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);

        return new ContentTemplateDto
        {
            Id = template.Id,
            BrandId = template.BrandId,
            Name = template.Name,
            PageType = template.PageType,
            Body = template.Body,
            IsGlobal = template.IsGlobal,
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
        };
    }
}
