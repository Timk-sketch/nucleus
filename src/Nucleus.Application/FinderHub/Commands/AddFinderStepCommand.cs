using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.FinderHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.FinderHub.Commands;

/// <summary>
/// Adds a new step (question screen) to an existing Finder.
/// Auto-assigns StepOrder as max(existing) + 1.
/// Returns the new FinderStepDto.
/// </summary>
public record AddFinderStepCommand(
    Guid FinderId,
    string QuestionText,
    string StepType = "single_choice",
    string? HelperText = null,
    bool IsRequired = true) : IRequest<FinderStepDto>;

public class AddFinderStepValidator : AbstractValidator<AddFinderStepCommand>
{
    private static readonly string[] ValidTypes =
        ["single_choice", "multi_choice", "text", "date", "number"];

    public AddFinderStepValidator()
    {
        RuleFor(x => x.FinderId).NotEmpty();
        RuleFor(x => x.QuestionText).NotEmpty().MaximumLength(500);
        RuleFor(x => x.StepType)
            .Must(t => ValidTypes.Contains(t))
            .WithMessage($"StepType must be one of: {string.Join(", ", ValidTypes)}.");
        RuleFor(x => x.HelperText).MaximumLength(500);
    }
}

public class AddFinderStepHandler : IRequestHandler<AddFinderStepCommand, FinderStepDto>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public AddFinderStepHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<FinderStepDto> Handle(
        AddFinderStepCommand request, CancellationToken cancellationToken)
    {
        var finder = await _db.Finders
            .FirstOrDefaultAsync(
                f => f.Id == request.FinderId && f.TenantId == _tenant.TenantId,
                cancellationToken)
            ?? throw new InvalidOperationException("Finder not found for this tenant.");

        // Calculate next step order
        var maxOrder = await _db.FinderSteps
            .Where(s => s.FinderId == request.FinderId)
            .Select(s => (int?)s.StepOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var step = new FinderStep
        {
            TenantId = _tenant.TenantId,
            FinderId = request.FinderId,
            StepOrder = maxOrder + 1,
            StepType = request.StepType,
            QuestionText = request.QuestionText.Trim(),
            HelperText = request.HelperText?.Trim(),
            IsRequired = request.IsRequired,
        };

        _db.FinderSteps.Add(step);
        await _db.SaveChangesAsync(cancellationToken);

        return new FinderStepDto
        {
            Id = step.Id,
            FinderId = step.FinderId,
            StepOrder = step.StepOrder,
            StepType = step.StepType,
            QuestionText = step.QuestionText,
            HelperText = step.HelperText,
            IsRequired = step.IsRequired,
            Options = [],
        };
    }
}
