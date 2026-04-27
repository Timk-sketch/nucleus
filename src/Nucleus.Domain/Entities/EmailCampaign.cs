namespace Nucleus.Domain.Entities;

public class EmailCampaign : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string Status { get; set; } = "draft"; // draft | sending | sent | failed
    public string? ToEmails { get; set; }          // comma-separated recipients
    public int RecipientCount { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}
