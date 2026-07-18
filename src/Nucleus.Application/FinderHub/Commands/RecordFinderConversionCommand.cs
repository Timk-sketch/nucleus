using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.FinderHub.Commands;

/// <summary>
/// Marks a FinderSession as converted (user clicked the CTA / became a lead).
/// Uses the EmbedToken + SessionToken for unauthenticated access.
/// Increments daily FinderAnalytics conversions.
/// Returns true if session found and marked, false if not found.
/// </summary>
public record RecordFinderConversionCommand(
    string EmbedToken,
    string SessionToken) : IRequest<bool>;

public class RecordFinderConversionValidator : AbstractValidator<RecordFinderConversionCommand>
{
    public RecordFinderConversionValidator()
    {
        RuleFor(x => x.EmbedToken).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SessionToken).NotEmpty().MaximumLength(100);
    }
}

public class RecordFinderConversionHandler : IRequestHandler<RecordFinderConversionCommand, bool>
{
    private readonly INucleusDbContext _db;

    public RecordFinderConversionHandler(INucleusDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(
        RecordFinderConversionCommand request, CancellationToken cancellationToken)
    {
        // Resolve finder
        var finder = await _db.Finders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                f => f.EmbedToken == request.EmbedToken && f.Status == "published",
                cancellationToken);

        if (finder is null)
            return false;

        // Find the session
        var session = await _db.FinderSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                s => s.SessionToken == request.SessionToken && s.FinderId == finder.Id,
                cancellationToken);

        if (session is null)
            return false;

        // Idempotent — only convert once
        if (session.Converted)
            return true;

        session.Converted = true;
        session.UpdatedAt = DateTimeOffset.UtcNow;

        // Increment daily conversions
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var analyticsRow = await _db.FinderAnalytics
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.FinderId == finder.Id && a.Date == today, cancellationToken);

        if (analyticsRow is null)
        {
            analyticsRow = new Domain.Entities.FinderAnalytics
            {
                TenantId = finder.TenantId,
                FinderId = finder.Id,
                Date = today,
                Conversions = 1,
            };
            _db.FinderAnalytics.Add(analyticsRow);
        }
        else
        {
            analyticsRow.Conversions++;
            analyticsRow.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
