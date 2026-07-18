namespace Nucleus.Domain.Entities;

/// <summary>
/// A custom domain (hostname) mapped to a brand's CMS site.
/// One brand can have multiple domains; one is primary.
/// Status: ssl_verified tracks whether domain DNS points to Nucleus.
/// </summary>
public class SiteDomain : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>Full hostname, e.g. "www.example.com" or "example.com".</summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>Whether this is the primary domain for the brand.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Whether SSL/TLS is verified and active for this hostname.</summary>
    public bool SslVerified { get; set; }

    /// <summary>When the domain was last successfully verified.</summary>
    public DateTimeOffset? VerifiedAt { get; set; }
}
