namespace Nucleus.Domain.Entities;

/// <summary>
/// A Finder is a multi-step quiz/product-finder widget that can be embedded on external pages.
/// Each Finder belongs to a Brand and contains Steps, Options, and Results.
/// Status: draft | published | archived
/// </summary>
public class Finder : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>Human-readable name shown in the admin UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-friendly slug (unique per brand).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Intro text shown at the start of the finder widget.</summary>
    public string? IntroText { get; set; }

    /// <summary>Lifecycle status: draft | published | archived.</summary>
    public string Status { get; set; } = "draft";

    /// <summary>When this finder was published (null if never published).</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>
    /// Secure random token used to embed and identify this finder publicly.
    /// Passed in the embed snippet URL; no auth required.
    /// </summary>
    public string EmbedToken { get; set; } = Guid.NewGuid().ToString("N");

    // Navigation
    public ICollection<FinderStep> Steps { get; set; } = new List<FinderStep>();
    public ICollection<FinderResult> Results { get; set; } = new List<FinderResult>();
    public ICollection<FinderSession> Sessions { get; set; } = new List<FinderSession>();
    public ICollection<FinderAnalytics> Analytics { get; set; } = new List<FinderAnalytics>();
}
