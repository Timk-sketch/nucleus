using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.FinderHub.DTOs;

namespace Nucleus.Application.FinderHub.Queries;

/// <summary>
/// Returns the list of Finders for a brand, scoped to the current tenant.
/// Includes step count and result count for each finder (list view).
/// </summary>
public record GetFindersQuery(Guid BrandId) : IRequest<List<FinderDto>>;

public class GetFindersHandler : IRequestHandler<GetFindersQuery, List<FinderDto>>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetFindersHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<FinderDto>> Handle(
        GetFindersQuery request, CancellationToken cancellationToken)
    {
        var finders = await _db.Finders
            .Where(f => f.BrandId == request.BrandId && f.TenantId == _tenant.TenantId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        if (finders.Count == 0)
            return [];

        var finderIds = finders.Select(f => f.Id).ToList();

        // Step counts
        var stepCounts = await _db.FinderSteps
            .Where(s => finderIds.Contains(s.FinderId))
            .GroupBy(s => s.FinderId)
            .Select(g => new { FinderId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var stepCountMap = stepCounts.ToDictionary(x => x.FinderId, x => x.Count);

        // Result counts
        var resultCounts = await _db.FinderResults
            .Where(r => finderIds.Contains(r.FinderId))
            .GroupBy(r => r.FinderId)
            .Select(g => new { FinderId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var resultCountMap = resultCounts.ToDictionary(x => x.FinderId, x => x.Count);

        return finders.Select(f => new FinderDto
        {
            Id = f.Id,
            BrandId = f.BrandId,
            Name = f.Name,
            Slug = f.Slug,
            IntroText = f.IntroText,
            Status = f.Status,
            PublishedAt = f.PublishedAt,
            EmbedToken = f.EmbedToken,
            StepCount = stepCountMap.GetValueOrDefault(f.Id, 0),
            ResultCount = resultCountMap.GetValueOrDefault(f.Id, 0),
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt,
        }).ToList();
    }
}
