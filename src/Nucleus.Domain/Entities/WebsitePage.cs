namespace Nucleus.Domain.Entities;

/// <summary>
/// A CMS page belonging to a brand's website.
/// Tracks slug, HTML content, SEO metadata, status, and schema JSON.
/// Status: draft | published | archived
/// </summary>
public class WebsitePage : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>URL slug (relative path), e.g. "about-us" or "services/registration".</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Human-readable page title (H1 / display title).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Page category: homepage | landing | blog | service | legal | other.</summary>
    public string PageType { get; set; } = "other";

    /// <summary>Full HTML content of the page.</summary>
    public string? HtmlContent { get; set; }

    /// <summary>SEO &lt;title&gt; tag value.</summary>
    public string? SeoTitle { get; set; }

    /// <summary>Meta description content.</summary>
    public string? MetaDescription { get; set; }

    /// <summary>Open Graph image URL.</summary>
    public string? OgImage { get; set; }

    /// <summary>Lifecycle status: draft | published | archived.</summary>
    public string Status { get; set; } = "draft";

    /// <summary>When this page was last published (null if never published).</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>JSON-LD schema markup for this page (stored as jsonb).</summary>
    public string? SchemaJson { get; set; }
}
