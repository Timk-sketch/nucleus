namespace Nucleus.Application.ContentHub.DTOs;

public class KeywordLibraryDto
{
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public int TotalKeywords { get; set; }
    public List<KeywordItemDto> Keywords { get; set; } = [];
}

public class KeywordItemDto
{
    public Guid Id { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public string? Notes { get; set; }
    public int? LatestPosition { get; set; }
    public int? PreviousPosition { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Number of ContentPages created targeting this keyword.</summary>
    public int ContentCount { get; set; }
}
