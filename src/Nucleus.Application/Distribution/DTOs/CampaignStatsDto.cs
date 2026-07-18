namespace Nucleus.Application.Distribution.DTOs;

public class CampaignStatsDto
{
    public Guid CampaignId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalMessages { get; set; }
    public int TotalRecipients { get; set; }
    public int TotalOpens { get; set; }
    public int TotalClicks { get; set; }
    public double OpenRate => TotalRecipients > 0 ? Math.Round((double)TotalOpens / TotalRecipients * 100, 1) : 0;
    public double ClickRate => TotalRecipients > 0 ? Math.Round((double)TotalClicks / TotalRecipients * 100, 1) : 0;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
