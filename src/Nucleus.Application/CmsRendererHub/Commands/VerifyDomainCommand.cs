using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.CmsRendererHub.DTOs;

namespace Nucleus.Application.CmsRendererHub.Commands;

/// <summary>
/// Verifies a custom domain by performing a DNS lookup.
/// Checks that the hostname resolves (stub implementation — always marks verified in dev).
/// Sets SslVerified=true and stamps VerifiedAt on success.
/// Returns the updated SiteDomainDto, or null if the domain was not found.
/// </summary>
public record VerifyDomainCommand(Guid DomainId) : IRequest<SiteDomainDto?>;

public class VerifyDomainValidator : AbstractValidator<VerifyDomainCommand>
{
    public VerifyDomainValidator()
    {
        RuleFor(x => x.DomainId).NotEmpty().WithMessage("DomainId is required.");
    }
}

public class VerifyDomainHandler : IRequestHandler<VerifyDomainCommand, SiteDomainDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public VerifyDomainHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SiteDomainDto?> Handle(
        VerifyDomainCommand request, CancellationToken cancellationToken)
    {
        var domain = await _db.SiteDomains
            .FirstOrDefaultAsync(d => d.Id == request.DomainId, cancellationToken);

        if (domain is null)
            return null;

        // DNS verification stub — in production this would call a DNS resolver
        // (e.g. System.Net.Dns.GetHostAddressesAsync) or check a CNAME record.
        // For now we mark as verified to enable end-to-end testing.
        var verified = await PerformDnsCheckAsync(domain.Hostname, cancellationToken);

        domain.SslVerified = verified;
        domain.VerifiedAt = verified ? DateTimeOffset.UtcNow : domain.VerifiedAt;
        domain.UpdatedAt = DateTimeOffset.UtcNow;

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

    /// <summary>
    /// Performs a DNS resolution check for the given hostname.
    /// Stub: resolves via System.Net.Dns — any resolvable hostname is considered verified.
    /// In production this would verify a specific CNAME/A record pointing to Nucleus.
    /// </summary>
    private static async Task<bool> PerformDnsCheckAsync(string hostname, CancellationToken ct)
    {
        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(hostname, ct);
            return addresses.Length > 0;
        }
        catch
        {
            // DNS lookup failed — domain is not verified
            return false;
        }
    }
}
