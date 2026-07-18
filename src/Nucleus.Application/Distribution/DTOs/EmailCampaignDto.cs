namespace Nucleus.Application.Distribution.DTOs;

public class EmailCampaignDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RecipientCount { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Message stats rolled up from EmailCampaignMessages.</summary>
    public int TotalMessages { get; set; }
    public int TotalOpens { get; set; }
    public int TotalClicks { get; set; }
}
