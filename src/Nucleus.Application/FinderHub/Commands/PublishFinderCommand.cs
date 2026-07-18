using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.FinderHub.Commands;

/// <summary>
/// Publishes a Finder, making it live for embedding.
/// Returns true if found and published, false if not found.
/// Requires at least 1 step with 1 option and 1 result.
/// </summary>
public record PublishFinderCommand(Guid FinderId) : IRequest<bool>;

public class PublishFinderValidator : AbstractValidator<PublishFinderCommand>
{
    public PublishFinderValidator()
    {
        RuleFor(x => x.FinderId).NotEmpty();
    }
}

public class PublishFinderHandler : IRequestHandler<PublishFinderCommand, bool>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public PublishFinderHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<bool> Handle(
        PublishFinderCommand request, CancellationToken cancellationToken)
    {
        var finder = await _db.Finders
            .FirstOrDefaultAsync(
                f => f.Id == request.FinderId && f.TenantId == _tenant.TenantId,
                cancellationToken);

        if (finder is null)
            return false;

        // Must have at least one step
        var stepCount = await _db.FinderSteps
            .CountAsync(s => s.FinderId == request.FinderId, cancellationToken);

        if (stepCount == 0)
            throw new InvalidOperationException("Cannot publish a finder with no steps. Add at least one step first.");

        // Must have at least one result
        var resultCount = await _db.FinderResults
            .CountAsync(r => r.FinderId == request.FinderId, cancellationToken);

        if (resultCount == 0)
            throw new InvalidOperationException("Cannot publish a finder with no results. Add at least one result first.");

        finder.Status = "published";
        finder.PublishedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
