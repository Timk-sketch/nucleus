using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.FinderHub.DTOs;

namespace Nucleus.Application.FinderHub.Commands;

/// <summary>
/// Updates the condition JSON and display fields of an existing FinderResult.
/// Returns null if the result is not found for this tenant.
/// </summary>
public record UpdateFinderResultCommand(
    Guid ResultId,
    string ProductKey,
    string Headline,
    string ConditionJson = "{}",
    string? Body = null,
    string? CtaLabel = null,
    string? CtaUrl = null) : IRequest<FinderResultDto?>;

public class UpdateFinderResultValidator : AbstractValidator<UpdateFinderResultCommand>
{
    public UpdateFinderResultValidator()
    {
        RuleFor(x => x.ResultId).NotEmpty();
        RuleFor(x => x.ProductKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Headline).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ConditionJson).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Body).MaximumLength(2000);
        RuleFor(x => x.CtaLabel).MaximumLength(100);
        RuleFor(x => x.CtaUrl).MaximumLength(500);
    }
}

public class UpdateFinderResultHandler : IRequestHandler<UpdateFinderResultCommand, FinderResultDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public UpdateFinderResultHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<FinderResultDto?> Handle(
        UpdateFinderResultCommand request, CancellationToken cancellationToken)
    {
        var result = await _db.FinderResults
            .FirstOrDefaultAsync(
                r => r.Id == request.ResultId && r.TenantId == _tenant.TenantId,
                cancellationToken);

        if (result is null)
            return null;

        result.ProductKey = request.ProductKey.Trim();
        result.Headline = request.Headline.Trim();
        result.ConditionJson = request.ConditionJson;
        result.Body = request.Body?.Trim();
        result.CtaLabel = request.CtaLabel?.Trim();
        result.CtaUrl = request.CtaUrl?.Trim();
        result.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return new FinderResultDto
        {
            Id = result.Id,
            FinderId = result.FinderId,
            ConditionJson = result.ConditionJson,
            ProductKey = result.ProductKey,
            Headline = result.Headline,
            Body = result.Body,
            CtaLabel = result.CtaLabel,
            CtaUrl = result.CtaUrl,
        };
    }
}
