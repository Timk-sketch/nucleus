namespace Nucleus.Application.AuthorityHub.DTOs;

public class BrandMentionDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string MentionText { get; set; } = string.Empty;
    public string Sentiment { get; set; } = "neutral";
    public DateTimeOffset DiscoveredAt { get; set; }
    public bool IsReviewed { get; set; }
}
