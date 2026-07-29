using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.FinderHub.Commands;

/// <summary>
/// Creates an A/B variant for a Finder. Requires agency plan.
/// </summary>
public record CreateFinderVariantCommand(
    Guid FinderId,
    string Name,
    string? IntroTextOverride,
    int Weight = 50) : IRequest<FinderVariantDto>;

public record FinderVariantDto
{
    public Guid Id { get; set; }
    public Guid FinderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IntroTextOverride { get; set; }
    public int Weight { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateFinderVariantValidator : AbstractValidator<CreateFinderVariantCommand>
{
    public CreateFinderVariantValidator()
    {
        RuleFor(x => x.FinderId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IntroTextOverride).MaximumLength(1000).When(x => x.IntroTextOverride != null);
        RuleFor(x => x.Weight).InclusiveBetween(1, 100);
    }
}

public class CreateFinderVariantHandler : IRequestHandler<CreateFinderVariantCommand, FinderVariantDto>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;
    private readonly ITenantPlanService _plan;

    public CreateFinderVariantHandler(
        INucleusDbContext db,
        ICurrentTenantService tenant,
        ITenantPlanService plan)
    {
        _db = db;
        _tenant = tenant;
        _plan = plan;
    }

    public async Task<FinderVariantDto> Handle(
        CreateFinderVariantCommand request, CancellationToken cancellationToken)
    {
        // Plan gate: A/B testing = agency only
        if (!await _plan.IsFeatureAllowedAsync("ab_testing", cancellationToken))
            throw new InvalidOperationException("A/B testing requires an Agency plan.");

        // Verify finder belongs to this tenant
        var finder = await _db.Finders
            .FirstOrDefaultAsync(f => f.Id == request.FinderId && f.TenantId == _tenant.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Finder not found.");

        var variant = new FinderVariant
        {
            TenantId = _tenant.TenantId,
            FinderId = finder.Id,
            Name = request.Name,
            IntroTextOverride = request.IntroTextOverride,
            Weight = request.Weight,
        };

        _db.FinderVariants.Add(variant);
        await _db.SaveChangesAsync(cancellationToken);

        return new FinderVariantDto
        {
            Id = variant.Id,
            FinderId = variant.FinderId,
            Name = variant.Name,
            IntroTextOverride = variant.IntroTextOverride,
            Weight = variant.Weight,
            CreatedAt = variant.CreatedAt,
        };
    }
}
