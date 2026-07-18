using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.Distribution.Commands;

/// <summary>
/// Creates a new email campaign in "draft" status.
/// An EmailCampaignMessage is also created as the initial message draft.
/// </summary>
public record CreateEmailCampaignCommand(
    Guid BrandId,
    string Subject,
    string HtmlBody,
    string? Name = null) : IRequest<Guid>;

public class CreateEmailCampaignValidator : AbstractValidator<CreateEmailCampaignCommand>
{
    public CreateEmailCampaignValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Subject)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("Subject must be 1–500 characters.");
        RuleFor(x => x.HtmlBody)
            .NotEmpty()
            .WithMessage("Email body is required.");
    }
}

public class CreateEmailCampaignHandler : IRequestHandler<CreateEmailCampaignCommand, Guid>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public CreateEmailCampaignHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateEmailCampaignCommand request, CancellationToken cancellationToken)
    {
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        var campaign = new EmailCampaign
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            Subject = request.Subject.Trim(),
            HtmlBody = request.HtmlBody.Trim(),
            Status = "draft",
        };

        _db.EmailCampaigns.Add(campaign);

        // Also create the initial EmailCampaignMessage
        var message = new EmailCampaignMessage
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            CampaignId = campaign.Id,
            Subject = campaign.Subject,
            HtmlBody = campaign.HtmlBody,
            Status = "draft",
        };

        _db.EmailCampaignMessages.Add(message);
        await _db.SaveChangesAsync(cancellationToken);

        return campaign.Id;
    }
}
