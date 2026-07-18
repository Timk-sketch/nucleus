namespace Nucleus.Domain.Entities;

/// <summary>
/// A reusable JSON-LD schema template for a specific page type.
/// PageType examples: "Article" | "FAQPage" | "HowTo" | "Service" | "LocalBusiness" | "Product"
/// SchemaType is the @type value to inject into the JSON-LD (mirrors or extends PageType).
/// </summary>
public class SchemaTemplate : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>The page type this template applies to (Article, FAQPage, HowTo, Service, LocalBusiness).</summary>
    public string PageType { get; set; } = string.Empty;

    /// <summary>The JSON-LD @type value, e.g. "FAQPage", "HowTo", "Article".</summary>
    public string SchemaType { get; set; } = string.Empty;

    /// <summary>The full JSON-LD template (stored as JSON string). May include {{token}} placeholders.</summary>
    public string TemplateJson { get; set; } = "{}";

    /// <summary>Whether this template is currently active and applied to matching pages.</summary>
    public bool IsActive { get; set; } = true;
}
