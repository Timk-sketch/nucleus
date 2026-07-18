using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.AuthorityHub.Commands;

/// <summary>
/// Adds a new prospect to the outreach queue for a brand.
/// Returns the new item Id.
/// Plan gate: outreach_queue = agency+
/// </summary>
public record AddOutreachItemCommand(
    Guid BrandId,
    string TargetUrl,
    string? ContactEmail = null,
    string? Notes = null) : IRequest<Guid>;

public class AddOutreachItemValidator : AbstractValidator<AddOutreachItemCommand>
{
    public AddOutreachItemValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.TargetUrl)
            .NotEmpty()
            .MaximumLength(1000)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("TargetUrl must be a valid absolute URL.");
        RuleFor(x => x.ContactEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail))
            .WithMessage("ContactEmail must be a valid email address.");
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class AddOutreachItemHandler : IRequestHandler<AddOutreachItemCommand, Guid>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public AddOutreachItemHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(
        AddOutreachItemCommand request, CancellationToken cancellationToken)
    {
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        var item = new OutreachQueueItem
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            TargetUrl = request.TargetUrl.Trim(),
            ContactEmail = request.ContactEmail?.Trim(),
            Notes = request.Notes?.Trim(),
            Status = "pending",
        };

        _db.OutreachQueueItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
