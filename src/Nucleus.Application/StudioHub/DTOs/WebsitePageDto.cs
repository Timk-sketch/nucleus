namespace Nucleus.Application.StudioHub.DTOs;

public class WebsitePageDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PageType { get; set; } = "other";
    public string? HtmlContent { get; set; }
    public string? SeoTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? OgImage { get; set; }
    public string Status { get; set; } = "draft";
    public DateTimeOffset? PublishedAt { get; set; }
    public string? SchemaJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class PageLibraryDto
{
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public int TotalPages { get; set; }
    public int PublishedPages { get; set; }
    public int DraftPages { get; set; }
    public List<WebsitePageDto> Pages { get; set; } = [];
}
