using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.FinderHub.Commands;

namespace Nucleus.Application.FinderHub.Queries;

/// <summary>Returns all A/B variants for a Finder, ordered by weight descending.</summary>
public record GetFinderVariantsQuery(Guid FinderId) : IRequest<List<FinderVariantDto>>;

public class GetFinderVariantsHandler : IRequestHandler<GetFinderVariantsQuery, List<FinderVariantDto>>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetFinderVariantsHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<FinderVariantDto>> Handle(
        GetFinderVariantsQuery request, CancellationToken cancellationToken)
    {
        return await _db.FinderVariants
            .Where(v => v.FinderId == request.FinderId && v.TenantId == _tenant.TenantId)
            .OrderByDescending(v => v.Weight)
            .Select(v => new FinderVariantDto
            {
                Id = v.Id,
                FinderId = v.FinderId,
                Name = v.Name,
                IntroTextOverride = v.IntroTextOverride,
                Weight = v.Weight,
                CreatedAt = v.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }
}
