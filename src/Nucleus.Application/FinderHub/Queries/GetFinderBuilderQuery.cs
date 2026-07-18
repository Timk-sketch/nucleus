using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.FinderHub.DTOs;

namespace Nucleus.Application.FinderHub.Queries;

/// <summary>
/// Returns the full builder view for a single Finder:
/// all steps (ordered by StepOrder) with their options (ordered by SortOrder),
/// and all results. Used by the admin builder UI.
/// Returns null if the finder doesn't belong to the current tenant.
/// </summary>
public record GetFinderBuilderQuery(Guid FinderId) : IRequest<FinderBuilderDto?>;

public class GetFinderBuilderHandler : IRequestHandler<GetFinderBuilderQuery, FinderBuilderDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetFinderBuilderHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<FinderBuilderDto?> Handle(
        GetFinderBuilderQuery request, CancellationToken cancellationToken)
    {
        var finder = await _db.Finders
            .FirstOrDefaultAsync(
                f => f.Id == request.FinderId && f.TenantId == _tenant.TenantId,
                cancellationToken);

        if (finder is null)
            return null;

        // Load steps with options
        var steps = await _db.FinderSteps
            .Where(s => s.FinderId == finder.Id)
            .OrderBy(s => s.StepOrder)
            .ToListAsync(cancellationToken);

        var stepIds = steps.Select(s => s.Id).ToList();

        var options = await _db.FinderOptions
            .Where(o => stepIds.Contains(o.StepId))
            .OrderBy(o => o.SortOrder)
            .ToListAsync(cancellationToken);

        var optionsByStep = options.GroupBy(o => o.StepId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load results
        var results = await _db.FinderResults
            .Where(r => r.FinderId == finder.Id)
            .ToListAsync(cancellationToken);

        return new FinderBuilderDto
        {
            Id = finder.Id,
            BrandId = finder.BrandId,
            Name = finder.Name,
            Slug = finder.Slug,
            IntroText = finder.IntroText,
            Status = finder.Status,
            PublishedAt = finder.PublishedAt,
            EmbedToken = finder.EmbedToken,
            CreatedAt = finder.CreatedAt,
            UpdatedAt = finder.UpdatedAt,
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
