using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.FinderHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.FinderHub.Commands;

/// <summary>
/// Adds a result to a Finder.
/// ConditionJson encodes the answer-matching rule, e.g.:
///   { "1": "car", "2": "new" }
///   Keys = StepOrder (as string), Values = option value that must match.
///   Array values are also supported for OR matching: { "1": ["car", "truck"] }
/// Returns the new FinderResultDto.
/// </summary>
public record AddFinderResultCommand(
    Guid FinderId,
    string ProductKey,
    string Headline,
    string ConditionJson = "{}",
    string? Body = null,
    string? CtaLabel = null,
    string? CtaUrl = null) : IRequest<FinderResultDto>;

public class AddFinderResultValidator : AbstractValidator<AddFinderResultCommand>
{
    public AddFinderResultValidator()
    {
        RuleFor(x => x.FinderId).NotEmpty();
        RuleFor(x => x.ProductKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Headline).NotEmpty().MaximumLength(300);
        RuleFor(x => x.ConditionJson).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Body).MaximumLength(2000);
        RuleFor(x => x.CtaLabel).MaximumLength(100);
        RuleFor(x => x.CtaUrl).MaximumLength(500);
    }
}

public class AddFinderResultHandler : IRequestHandler<AddFinderResultCommand, FinderResultDto>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public AddFinderResultHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<FinderResultDto> Handle(
        AddFinderResultCommand request, CancellationToken cancellationToken)
    {
        var finderExists = await _db.Finders
            .AnyAsync(f => f.Id == request.FinderId && f.TenantId == _tenant.TenantId,
                cancellationToken);

        if (!finderExists)
            throw new InvalidOperationException("Finder not found for this tenant.");

        var result = new FinderResult
        {
            TenantId = _tenant.TenantId,
            FinderId = request.FinderId,
            ProductKey = request.ProductKey.Trim(),
            Headline = request.Headline.Trim(),
            ConditionJson = request.ConditionJson,
            Body = request.Body?.Trim(),
            CtaLabel = request.CtaLabel?.Trim(),
            CtaUrl = request.CtaUrl?.Trim(),
        };

        _db.FinderResults.Add(result);
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
