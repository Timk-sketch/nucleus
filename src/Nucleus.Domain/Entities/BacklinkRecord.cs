namespace Nucleus.Domain.Entities;

/// <summary>
/// A backlink discovered pointing to a brand's domain.
/// Tracks source URL, anchor text, domain rating, and active status.
/// Status is derived from is_active — false means the link was lost.
/// </summary>
public class BacklinkRecord : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>The page that contains the link (referring page).</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>The page on our domain being linked to.</summary>
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>The anchor text of the link.</summary>
    public string? AnchorText { get; set; }

    /// <summary>Domain Rating of the referring domain (0–100 scale, e.g. Ahrefs DR).</summary>
    public decimal? DomainRating { get; set; }

    /// <summary>When this backlink was first detected.</summary>
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When this backlink was most recently confirmed active.</summary>
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>False if this backlink has been lost (no longer live).</summary>
    public bool IsActive { get; set; } = true;
}
