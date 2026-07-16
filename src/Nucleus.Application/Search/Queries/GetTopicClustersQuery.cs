using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.Search.DTOs;
using System.Text.Json;

namespace Nucleus.Application.Search.Queries;

/// <summary>
/// Returns all topic clusters for a brand.
/// </summary>
public record GetTopicClustersQuery(Guid BrandId) : IRequest<List<TopicClusterDto>>;

public class GetTopicClustersHandler : IRequestHandler<GetTopicClustersQuery, List<TopicClusterDto>>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetTopicClustersHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<TopicClusterDto>> Handle(
        GetTopicClustersQuery request, CancellationToken cancellationToken)
    {
        var clusters = await _db.TopicClusters
            .Where(c => c.BrandId == request.BrandId && c.TenantId == _tenant.TenantId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return clusters.Select(c =>
        {
            List<string> keywords;
            try { keywords = JsonSerializer.Deserialize<List<string>>(c.ClusterKeywordsJson) ?? []; }
            catch { keywords = []; }

            return new TopicClusterDto
            {
                Id = c.Id,
                BrandId = c.BrandId,
                Name = c.Name,
                PillarKeyword = c.PillarKeyword,
                ClusterKeywords = keywords,
                Status = c.Status,
                Notes = c.Notes,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
            };
        }).ToList();
    }
}
