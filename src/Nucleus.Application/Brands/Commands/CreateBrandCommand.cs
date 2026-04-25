using FluentValidation;
using MediatR;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.Brands.Commands;

public record CreateBrandCommand(
    string Code,
    string Name,
    string Domain,
    string PrimaryColor,
    string? WpSiteUrl,
    string? WpUsername,
    string? WpAppPassword,
    string? GhlLocationId,
    string? GhlApiKey,
    string? BrandVoice) : IRequest<CreateBrandResult>;

public record CreateBrandResult(Guid BrandId, string Status);

public class CreateBrandCommandValidator : AbstractValidator<CreateBrandCommand>
{
    public CreateBrandCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20).Matches("^[a-z0-9_-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Domain).NotEmpty().MaximumLength(253);
        RuleFor(x => x.PrimaryColor).Matches("^#[0-9A-Fa-f]{6}$").When(x => x.PrimaryColor != null);
    }
}

public class CreateBrandCommandHandler : IRequestHandler<CreateBrandCommand, CreateBrandResult>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public CreateBrandCommandHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<CreateBrandResult> Handle(CreateBrandCommand request, CancellationToken cancellationToken)
    {
        var brand = new Brand
        {
            TenantId = _tenant.TenantId,
            Code = request.Code,
            Name = request.Name,
            Domain = request.Domain,
            Slug = request.Domain.Replace(".", "-").ToLowerInvariant(),
            PrimaryColor = request.PrimaryColor,
            WpSiteUrl = request.WpSiteUrl,
            WpUsername = request.WpUsername,
            WpAppPassword = request.WpAppPassword,     // encrypted by EF value converter
            GhlLocationId = request.GhlLocationId,
            GhlApiKey = request.GhlApiKey,             // encrypted by EF value converter
            BrandVoice = request.BrandVoice,
            Status = "onboarding",
        };

        // Seed empty provisioning steps
        var steps = new[] { "wordpress", "ghl", "dataforseo", "backlinks", "email" };
        foreach (var step in steps)
        {
            brand.ProvisioningSteps.Add(new BrandProvisioningStep
            {
                TenantId = _tenant.TenantId,
                BrandId = brand.Id,
                StepName = step,
                Status = "pending",
            });
        }

        _db.Brands.Add(brand);
        await _db.SaveChangesAsync(cancellationToken);

        return new CreateBrandResult(brand.Id, brand.Status);
    }
}
