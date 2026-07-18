using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.CmsRendererHub.DTOs;

namespace Nucleus.Application.CmsRendererHub.Queries;

/// <summary>
/// Returns all custom domains mapped to a brand, ordered by IsPrimary desc then Hostname.
/// Also resolves a single domain from a hostname (used by the public renderer host-header lookup).
/// </summary>
public record GetCustomDomainQuery(
    Guid? BrandId = null,
    string? Hostname = null) : IRequest<GetCustomDomainResult>;

public class GetCustomDomainResult
{
    /// <summary>All domains for the brand (populated when BrandId is provided).</summary>
    public List<SiteDomainDto> Domains { get; set; } = [];

    /// <summary>The resolved BrandId when looking up by hostname (null if not found).</summary>
    public Guid? ResolvedBrandId { get; set; }
}

public class GetCustomDomainHandler : IRequestHandler<GetCustomDomainQuery, GetCustomDomainResult>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetCustomDomainHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<GetCustomDomainResult> Handle(
        GetCustomDomainQuery request, CancellationToken cancellationToken)
    {
        var result = new GetCustomDomainResult();

        // Hostname lookup (used by public renderer — bypasses tenant filter)
        if (!string.IsNullOrWhiteSpace(request.Hostname))
        {
            var hostname = request.Hostname.Trim().ToLowerInvariant();
            var match = await _db.SiteDomains
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(d => d.Hostname == hostname, cancellationToken);

            result.ResolvedBrandId = match?.BrandId;
        }

        // Brand domain list (tenant-scoped)
        if (request.BrandId.HasValue && request.BrandId.Value != Guid.Empty)
        {
            var domains = await _db.SiteDomains
                .Where(d => d.BrandId == request.BrandId.Value)
                .OrderByDescending(d => d.IsPrimary)
                .ThenBy(d => d.Hostname)
                .ToListAsync(cancellationToken);

            result.Domains = domains.Select(d => new SiteDomainDto
            {
                Id = d.Id,
                BrandId = d.BrandId,
                Hostname = d.Hostname,
                IsPrimary = d.IsPrimary,
                SslVerified = d.SslVerified,
                VerifiedAt = d.VerifiedAt,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt,
            }).ToList();
        }

        return result;
    }
}
