using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.CmsRendererHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.CmsRendererHub.Commands;

/// <summary>
/// Maps a custom hostname to a brand's CMS site.
/// If IsPrimary=true, demotes any existing primary domain for this brand.
/// Returns the newly created SiteDomainDto.
/// </summary>
public record MapCustomDomainCommand(
    Guid BrandId,
    string Hostname,
    bool IsPrimary = false) : IRequest<SiteDomainDto>;

public class MapCustomDomainValidator : AbstractValidator<MapCustomDomainCommand>
{
    public MapCustomDomainValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty().WithMessage("BrandId is required.");
        RuleFor(x => x.Hostname)
            .NotEmpty()
            .MaximumLength(300)
            .Must(h => Uri.CheckHostName(h) != UriHostNameType.Unknown)
            .WithMessage("Hostname must be a valid domain name (e.g. www.example.com).");
    }
}

public class MapCustomDomainHandler : IRequestHandler<MapCustomDomainCommand, SiteDomainDto>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public MapCustomDomainHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SiteDomainDto> Handle(
        MapCustomDomainCommand request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId,
                cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        var hostname = request.Hostname.Trim().ToLowerInvariant();

        // Check hostname uniqueness across ALL tenants (hostnames are globally unique)
        var hostnameTaken = await _db.SiteDomains
            .IgnoreQueryFilters()
            .AnyAsync(d => d.Hostname == hostname, cancellationToken);

        if (hostnameTaken)
            throw new InvalidOperationException(
                $"The hostname '{hostname}' is already mapped to another brand.");

        // If setting as primary, demote existing primary
        if (request.IsPrimary)
        {
            var existingPrimary = await _db.SiteDomains
                .Where(d => d.BrandId == request.BrandId && d.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var d in existingPrimary)
                d.IsPrimary = false;
        }

        var domain = new SiteDomain
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            Hostname = hostname,
            IsPrimary = request.IsPrimary,
            SslVerified = false,
        };

        _db.SiteDomains.Add(domain);
        await _db.SaveChangesAsync(cancellationToken);

        return new SiteDomainDto
        {
            Id = domain.Id,
            BrandId = domain.BrandId,
            Hostname = domain.Hostname,
            IsPrimary = domain.IsPrimary,
            SslVerified = domain.SslVerified,
            VerifiedAt = domain.VerifiedAt,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
        };
    }
}
