using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.FinderHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.FinderHub.Commands;

/// <summary>
/// Adds a new option to a FinderStep.
/// Auto-assigns SortOrder as max(existing) + 1.
/// Returns the new FinderOptionDto.
/// </summary>
public record AddFinderOptionCommand(
    Guid StepId,
    string Label,
    string Value,
    string? IconUrl = null,
    string? Description = null) : IRequest<FinderOptionDto>;

public class AddFinderOptionValidator : AbstractValidator<AddFinderOptionCommand>
{
    public AddFinderOptionValidator()
    {
        RuleFor(x => x.StepId).NotEmpty();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value)
            .NotEmpty()
            .MaximumLength(200)
            .Matches(@"^[a-zA-Z0-9_\-]+$")
            .WithMessage("Value must contain only alphanumeric characters, hyphens, or underscores.");
        RuleFor(x => x.IconUrl).MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

public class AddFinderOptionHandler : IRequestHandler<AddFinderOptionCommand, FinderOptionDto>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public AddFinderOptionHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<FinderOptionDto> Handle(
        AddFinderOptionCommand request, CancellationToken cancellationToken)
    {
        // Verify step belongs to this tenant
        var step = await _db.FinderSteps
            .FirstOrDefaultAsync(
                s => s.Id == request.StepId && s.TenantId == _tenant.TenantId,
                cancellationToken)
            ?? throw new InvalidOperationException("FinderStep not found for this tenant.");

        // Auto-assign sort order
        var maxSort = await _db.FinderOptions
            .Where(o => o.StepId == request.StepId)
            .Select(o => (int?)o.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        var option = new FinderOption
        {
            TenantId = _tenant.TenantId,
            StepId = request.StepId,
            Label = request.Label.Trim(),
            Value = request.Value.Trim().ToLowerInvariant(),
            IconUrl = request.IconUrl?.Trim(),
            Description = request.Description?.Trim(),
            SortOrder = maxSort + 1,
        };

        _db.FinderOptions.Add(option);
        await _db.SaveChangesAsync(cancellationToken);

        return new FinderOptionDto
        {
            Id = option.Id,
            StepId = option.StepId,
            Label = option.Label,
            Value = option.Value,
            IconUrl = option.IconUrl,
            Description = option.Description,
            SortOrder = option.SortOrder,
        };
    }
}
