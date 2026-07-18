namespace Nucleus.Application.CmsRendererHub.DTOs;

/// <summary>
/// The rendered output returned for a public page request (GET /cms/{slug}).
/// Contains the full rendered HTML and HTTP caching metadata.
/// </summary>
public class PublicPageDto
{
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? SeoTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? OgImage { get; set; }
    public string? SchemaJson { get; set; }
    public string RenderedHtml { get; set; } = string.Empty;
    public string Etag { get; set; } = string.Empty;
    public DateTimeOffset CachedAt { get; set; }
    public bool ServedFromCache { get; set; }
}
