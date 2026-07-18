using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.FinderHub.Commands;

/// <summary>
/// Creates a new Finder (quiz) for a brand.
/// Returns the new Finder Id.
/// Plan gate: finder_count — starter=1, pro=5, agency=unlimited.
/// </summary>
public record CreateFinderCommand(
    Guid BrandId,
    string Name,
    string Slug,
    string? IntroText = null) : IRequest<Guid>;

public class CreateFinderValidator : AbstractValidator<CreateFinderCommand>
{
    public CreateFinderValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(200)
            .Matches(@"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase alphanumeric with hyphens only.");
        RuleFor(x => x.IntroText).MaximumLength(1000);
    }
}

public class CreateFinderHandler : IRequestHandler<CreateFinderCommand, Guid>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public CreateFinderHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(
        CreateFinderCommand request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId,
                cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        // Enforce slug uniqueness per brand
        var slugTaken = await _db.Finders
            .AnyAsync(f => f.BrandId == request.BrandId && f.Slug == request.Slug,
                cancellationToken);

        if (slugTaken)
            throw new InvalidOperationException($"A finder with slug '{request.Slug}' already exists for this brand.");

        var finder = new Finder
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            Name = request.Name.Trim(),
            Slug = request.Slug.Trim().ToLowerInvariant(),
            IntroText = request.IntroText?.Trim(),
            Status = "draft",
            EmbedToken = Guid.NewGuid().ToString("N"),
        };

        _db.Finders.Add(finder);
        await _db.SaveChangesAsync(cancellationToken);

        return finder.Id;
    }
}
