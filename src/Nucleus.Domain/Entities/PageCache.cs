namespace Nucleus.Domain.Entities;

/// <summary>
/// Caches the rendered HTML for a WebsitePage slug, keyed by BrandId + Slug.
/// Cache hit path target: &lt;5ms response time.
/// Invalidated by: new deploy, explicit invalidation, or page update.
/// </summary>
public class PageCache : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>URL slug for this cached page, e.g. "about-us" or "services/registration".</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Full rendered HTML for the page.</summary>
    public string RenderedHtml { get; set; } = string.Empty;

    /// <summary>HTTP ETag for conditional GET support (MD5/hash of rendered content).</summary>
    public string Etag { get; set; } = string.Empty;

    /// <summary>When this cache entry was last written.</summary>
    public DateTimeOffset CachedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When this cache entry was invalidated (null = still valid).</summary>
    public DateTimeOffset? InvalidatedAt { get; set; }
}
