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

    // ── Sprint 32: White-label embed (agency plan) ─────────────────────────

    /// <summary>Enables white-label mode — hides Nucleus branding from the embed widget.</summary>
    public bool WhiteLabelEnabled { get; set; }

    /// <summary>Custom CSS injected into the embed widget (agency plan only).</summary>
    public string? CustomCss { get; set; }

    /// <summary>Logo URL shown in the embed widget header when white-label is enabled.</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Primary color override for the embed widget (hex, e.g. "#6366f1").</summary>
    public string? PrimaryColorOverride { get; set; }

    // Navigation
    public ICollection<FinderStep> Steps { get; set; } = new List<FinderStep>();
    public ICollection<FinderResult> Results { get; set; } = new List<FinderResult>();
    public ICollection<FinderSession> Sessions { get; set; } = new List<FinderSession>();
    public ICollection<FinderAnalytics> Analytics { get; set; } = new List<FinderAnalytics>();
    public ICollection<FinderVariant> Variants { get; set; } = new List<FinderVariant>();
}
