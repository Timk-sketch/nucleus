using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.FinderHub.DTOs;

namespace Nucleus.Application.FinderHub.Queries;

/// <summary>
/// Returns the full public config for a Finder identified by its EmbedToken.
/// No authentication required — uses IgnoreQueryFilters for global lookup.
/// Returns null if no published finder exists with that token.
/// Used by the embeddable widget and standalone preview.
/// </summary>
public record GetPublicFinderQuery(string EmbedToken) : IRequest<PublicFinderDto?>;

public class GetPublicFinderHandler : IRequestHandler<GetPublicFinderQuery, PublicFinderDto?>
{
    private readonly INucleusDbContext _db;

    public GetPublicFinderHandler(INucleusDbContext db)
    {
        _db = db;
    }

    public async Task<PublicFinderDto?> Handle(
        GetPublicFinderQuery request, CancellationToken cancellationToken)
    {
        // Global lookup — no tenant filter (EmbedToken is the security boundary)
        var finder = await _db.Finders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                f => f.EmbedToken == request.EmbedToken && f.Status == "published",
                cancellationToken);

        if (finder is null)
            return null;

        // Load steps
        var steps = await _db.FinderSteps
            .IgnoreQueryFilters()
            .Where(s => s.FinderId == finder.Id)
            .OrderBy(s => s.StepOrder)
            .ToListAsync(cancellationToken);

        var stepIds = steps.Select(s => s.Id).ToList();

        var options = await _db.FinderOptions
            .IgnoreQueryFilters()
            .Where(o => stepIds.Contains(o.StepId))
            .OrderBy(o => o.SortOrder)
            .ToListAsync(cancellationToken);

        var optionsByStep = options.GroupBy(o => o.StepId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load results
        var results = await _db.FinderResults
            .IgnoreQueryFilters()
            .Where(r => r.FinderId == finder.Id)
            .ToListAsync(cancellationToken);

        return new PublicFinderDto
        {
            Id = finder.Id,
            Name = finder.Name,
            Slug = finder.Slug,
            IntroText = finder.IntroText,
            EmbedToken = finder.EmbedToken,
            Steps = steps.Select(s => new FinderStepDto
            {
                Id = s.Id,
                FinderId = s.FinderId,
                StepOrder = s.StepOrder,
                StepType = s.StepType,
                QuestionText = s.QuestionText,
                HelperText = s.HelperText,
                IsRequired = s.IsRequired,
                Options = optionsByStep.TryGetValue(s.Id, out var opts)
                    ? opts.Select(o => new FinderOptionDto
                    {
                        Id = o.Id,
                        StepId = o.StepId,
                        Label = o.Label,
                        Value = o.Value,
                        IconUrl = o.IconUrl,
                        Description = o.Description,
                        SortOrder = o.SortOrder,
                    }).ToList()
                    : [],
            }).ToList(),
            Results = results.Select(r => new FinderResultDto
            {
                Id = r.Id,
                FinderId = r.FinderId,
                ConditionJson = r.ConditionJson,
                ProductKey = r.ProductKey,
                Headline = r.Headline,
                Body = r.Body,
                CtaLabel = r.CtaLabel,
                CtaUrl = r.CtaUrl,
            }).ToList(),
        };
    }
}
