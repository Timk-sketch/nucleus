using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.FinderHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.FinderHub.Commands;

/// <summary>
/// Creates or updates a FinderSession for an anonymous user interacting with an embedded Finder.
/// Uses the EmbedToken (no auth required) to identify the Finder.
///
/// If SessionToken is provided and matches an existing session, updates AnswersJson.
/// If SessionToken is null or unknown, creates a new session with weighted A/B variant assignment.
///
/// When IsComplete=true:
///   - Runs result matching against FinderResults
///   - Sets ResultKey + CompletedAt on the session
///   - Increments FinderAnalytics completions for today
///
/// Increments FinderAnalytics starts count when a new session is created.
/// Lead fields (LeadName/LeadEmail/LeadPhone) are set when a lead_capture step is submitted.
/// Returns the FinderSessionDto.
/// </summary>
public record RecordFinderSessionCommand(
    string EmbedToken,
    string AnswersJson,
    string? SessionToken = null,
    bool IsComplete = false,
    string? LeadName = null,
    string? LeadEmail = null,
    string? LeadPhone = null) : IRequest<FinderSessionDto>;

public class RecordFinderSessionValidator : AbstractValidator<RecordFinderSessionCommand>
{
    public RecordFinderSessionValidator()
    {
        RuleFor(x => x.EmbedToken).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AnswersJson).NotEmpty().MaximumLength(8000);
        RuleFor(x => x.LeadEmail).MaximumLength(300).EmailAddress()
            .When(x => !string.IsNullOrEmpty(x.LeadEmail));
        RuleFor(x => x.LeadName).MaximumLength(200).When(x => x.LeadName != null);
        RuleFor(x => x.LeadPhone).MaximumLength(50).When(x => x.LeadPhone != null);
    }
}

public class RecordFinderSessionHandler : IRequestHandler<RecordFinderSessionCommand, FinderSessionDto>
{
    private readonly INucleusDbContext _db;

    public RecordFinderSessionHandler(INucleusDbContext db)
    {
        _db = db;
    }

    public async Task<FinderSessionDto> Handle(
        RecordFinderSessionCommand request, CancellationToken cancellationToken)
    {
        // Resolve finder by embed token — no auth required, global lookup
        var finder = await _db.Finders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                f => f.EmbedToken == request.EmbedToken && f.Status == "published",
                cancellationToken)
            ?? throw new InvalidOperationException($"No published finder found for embed token '{request.EmbedToken}'.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        FinderSession? session = null;

        // Try to resume an existing session
        if (!string.IsNullOrEmpty(request.SessionToken))
        {
            session = await _db.FinderSessions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    s => s.SessionToken == request.SessionToken && s.FinderId == finder.Id,
                    cancellationToken);
        }

        if (session is null)
        {
            // Assign A/B variant via weighted random selection
            var variantId = await AssignVariantAsync(finder.Id, cancellationToken);

            // Create new session
            session = new FinderSession
            {
                TenantId = finder.TenantId,
                FinderId = finder.Id,
                SessionToken = Guid.NewGuid().ToString("N"),
                AnswersJson = request.AnswersJson,
                VariantId = variantId,
            };
            _db.FinderSessions.Add(session);

            // Increment daily starts
            await IncrementAnalyticsAsync(finder.Id, finder.TenantId, today,
                starts: 1, completions: 0, conversions: 0, cancellationToken);
        }
        else
        {
            // Update existing session answers
            session.AnswersJson = request.AnswersJson;
            session.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // Capture lead info from lead_capture step
        if (!string.IsNullOrEmpty(request.LeadName))  session.LeadName  = request.LeadName;
        if (!string.IsNullOrEmpty(request.LeadEmail)) session.LeadEmail = request.LeadEmail;
        if (!string.IsNullOrEmpty(request.LeadPhone)) session.LeadPhone = request.LeadPhone;

        // Handle completion
        if (request.IsComplete && session.CompletedAt is null)
        {
            // Match result
            var results = await _db.FinderResults
                .IgnoreQueryFilters()
                .Where(r => r.FinderId == finder.Id)
                .ToListAsync(cancellationToken);

            session.ResultKey = FinderResultMatcher.Match(request.AnswersJson, results);
            session.CompletedAt = DateTimeOffset.UtcNow;

            // Increment daily completions
            await IncrementAnalyticsAsync(finder.Id, finder.TenantId, today,
                starts: 0, completions: 1, conversions: 0, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new FinderSessionDto
        {
            Id = session.Id,
            FinderId = session.FinderId,
            SessionToken = session.SessionToken,
            AnswersJson = session.AnswersJson,
            ResultKey = session.ResultKey,
            Converted = session.Converted,
            CompletedAt = session.CompletedAt,
            CreatedAt = session.CreatedAt,
        };
    }

    /// <summary>Weighted random variant assignment. Returns null when no variants are defined.</summary>
    private async Task<Guid?> AssignVariantAsync(Guid finderId, CancellationToken ct)
    {
        var variants = await _db.FinderVariants
            .IgnoreQueryFilters()
            .Where(v => v.FinderId == finderId)
            .Select(v => new { v.Id, v.Weight })
            .ToListAsync(ct);

        if (variants.Count == 0) return null;

        var totalWeight = variants.Sum(v => v.Weight);
        if (totalWeight <= 0) return variants[0].Id;

        var roll = Random.Shared.Next(0, totalWeight);
        var cumulative = 0;
        foreach (var v in variants)
        {
            cumulative += v.Weight;
            if (roll < cumulative) return v.Id;
        }

        return variants[^1].Id;
    }

    private async Task IncrementAnalyticsAsync(
        Guid finderId, Guid tenantId, DateOnly date,
        int starts, int completions, int conversions,
        CancellationToken ct)
    {
        var row = await _db.FinderAnalytics
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.FinderId == finderId && a.Date == date, ct);

        if (row is null)
        {
            row = new FinderAnalytics
            {
                TenantId = tenantId,
                FinderId = finderId,
                Date = date,
                Starts = starts,
                Completions = completions,
                Conversions = conversions,
            };
            _db.FinderAnalytics.Add(row);
        }
        else
        {
            row.Starts += starts;
            row.Completions += completions;
            row.Conversions += conversions;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
