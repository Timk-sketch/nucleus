namespace Nucleus.Application.Distribution.DTOs;

public class SendLogDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public Guid? CampaignId { get; set; }
    public Guid? SocialPostId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public int RecipientCount { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
