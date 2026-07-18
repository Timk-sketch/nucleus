namespace Nucleus.Application.FinderHub.DTOs;

/// <summary>Summary DTO for a Finder (list view).</summary>
public class FinderDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? IntroText { get; set; }
    public string Status { get; set; } = "draft";
    public DateTimeOffset? PublishedAt { get; set; }
    public string EmbedToken { get; set; } = string.Empty;
    public int StepCount { get; set; }
    public int ResultCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
