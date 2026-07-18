namespace Nucleus.Domain.Entities;

/// <summary>
/// Represents a piece of generated or manually created content for a brand.
/// Tracks status through the editorial workflow: draft → review → approved → published.
/// </summary>
public class ContentPage : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>Optional: the keyword this content targets.</summary>
    public Guid? KeywordId { get; set; }
    public BrandKeyword? Keyword { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>page_type: "blog_post" | "landing_page" | "service_page" | "pillar" | "cluster" | "other"</summary>
    public string PageType { get; set; } = "blog_post";

    /// <summary>status: "draft" | "review" | "approved" | "published" | "rejected"</summary>
    public string Status { get; set; } = "draft";

    public string? HtmlContent { get; set; }
    public string? SeoTitle { get; set; }
    public string? MetaDescription { get; set; }

    /// <summary>The AI model and prompt used to generate this content (null if written manually).</summary>
    public string? AiModel { get; set; }
    public string? AiPrompt { get; set; }

    /// <summary>Optional target word count requested from AI generation.</summary>
    public int? WordCount { get; set; }

    /// <summary>Scheduled publication date (used on editorial calendar).</summary>
    public DateTimeOffset? ScheduledAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Notes from reviewer during approval workflow.</summary>
    public string? ReviewNotes { get; set; }
}
