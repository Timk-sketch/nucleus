namespace Nucleus.Domain.Entities;

/// <summary>
/// Reusable content templates that provide structure / prompts for AI content generation.
/// Templates can be global (shared across all brands in a tenant) or brand-specific.
/// </summary>
public class ContentTemplate : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>page_type: "blog_post" | "landing_page" | "service_page" | "pillar" | "other"</summary>
    public string PageType { get; set; } = "blog_post";

    /// <summary>The template body — may include {{keyword}}, {{brand}}, {{service}} placeholders.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>If true, this template is shared across all brands in the tenant.</summary>
    public bool IsGlobal { get; set; } = false;

    public bool IsActive { get; set; } = true;
}
