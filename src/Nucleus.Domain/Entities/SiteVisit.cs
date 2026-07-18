namespace Nucleus.Domain.Entities;

/// <summary>
/// Records a public page visit for CMS analytics.
/// IP addresses are stored as hashed values (SHA-256) for privacy compliance.
/// </summary>
public class SiteVisit : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>The slug of the page that was visited.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>HTTP Referer header value (null if direct/unknown).</summary>
    public string? Referrer { get; set; }

    /// <summary>User-Agent header string (truncated to 500 chars).</summary>
    public string? UserAgent { get; set; }

    /// <summary>SHA-256 hash of the visitor's IP address for privacy compliance.</summary>
    public string? IpHash { get; set; }

    /// <summary>When the visit occurred.</summary>
    public DateTimeOffset VisitedAt { get; set; } = DateTimeOffset.UtcNow;
}
