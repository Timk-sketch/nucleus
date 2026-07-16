namespace Nucleus.Application.Search.DTOs;

public class ContentGapDto
{
    public Guid KeywordId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public int? CurrentPosition { get; set; }
    public int? SearchVolume { get; set; }
    public string? RankedUrl { get; set; }
    /// <summary>True when no TargetUrl is set — meaning no dedicated content exists for this keyword.</summary>
    public bool HasDedicatedContent { get; set; }
    public string? TargetUrl { get; set; }
}
