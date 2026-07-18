namespace Nucleus.Application.ContentHub.DTOs;

public class ContentPageDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public Guid? KeywordId { get; set; }
    public string? KeywordText { get; set; }
    public string Title { get; set; } = string.Empty;
    public string PageType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? HtmlContent { get; set; }
    public string? SeoTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? AiModel { get; set; }
    public int? WordCount { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
