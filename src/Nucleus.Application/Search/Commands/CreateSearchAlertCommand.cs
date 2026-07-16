using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.Search.Commands;

/// <summary>
/// Creates a search alert rule for a keyword. Fires when position crosses threshold.
/// </summary>
public record CreateSearchAlertCommand(
    Guid BrandId,
    Guid KeywordId,
    string AlertType,
    int Threshold) : IRequest<Guid>;

public class CreateSearchAlertValidator : AbstractValidator<CreateSearchAlertCommand>
{
    private static readonly string[] ValidAlertTypes =
        ["rank_drop", "rank_rise", "out_of_top_10", "entered_top_3"];

    public CreateSearchAlertValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.KeywordId).NotEmpty();
        RuleFor(x => x.AlertType)
            .NotEmpty()
            .Must(t => ValidAlertTypes.Contains(t))
            .WithMessage($"AlertType must be one of: {string.Join(", ", ValidAlertTypes)}");
        RuleFor(x => x.Threshold)
            .InclusiveBetween(1, 200)
            .WithMessage("Threshold must be between 1 and 200.");
    }
}

public class CreateSearchAlertHandler : IRequestHandler<CreateSearchAlertCommand, Guid>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public CreateSearchAlertHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateSearchAlertCommand request, CancellationToken cancellationToken)
    {
        // Ensure keyword belongs to this tenant's brand
        var keyword = await _db.BrandKeywords
            .Where(k => k.Id == request.KeywordId
                     && k.BrandId == request.BrandId
                     && k.TenantId == _tenant.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Keyword not found for this brand.");

        var alert = new SearchAlert
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            KeywordId = request.KeywordId,
            AlertType = request.AlertType,
            Threshold = request.Threshold,
            IsActive = true,
        };

        _db.SearchAlerts.Add(alert);
        await _db.SaveChangesAsync(cancellationToken);

        return alert.Id;
    }
}
