namespace Nucleus.Domain.Entities;

/// <summary>
/// A scheduled or published social media post for a brand.
/// Platform examples: "facebook", "instagram", "twitter", "linkedin", "google_my_business"
/// Status: "draft" | "scheduled" | "published" | "failed" | "cancelled"
/// </summary>
public class SocialPost : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>Optional link back to a content page this post promotes.</summary>
    public Guid? ContentPageId { get; set; }

    /// <summary>Target social platform (facebook, instagram, twitter, linkedin, gmb).</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>Post caption / body text.</summary>
    public string Caption { get; set; } = string.Empty;

    /// <summary>Optional image URL to attach to the post.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>When this post is scheduled to go out.</summary>
    public DateTimeOffset? ScheduledAt { get; set; }

    /// <summary>When the post was actually published.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>draft | scheduled | published | failed | cancelled</summary>
    public string Status { get; set; } = "draft";

    /// <summary>The external post ID from GHL Social Planner (or equivalent provider).</summary>
    public string? ExternalPostId { get; set; }

    /// <summary>Error details when Status == "failed".</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Publishing provider used: "ghl" | "vista" | "manual"</summary>
    public string? Provider { get; set; }
}
