namespace Nucleus.Domain.Entities;

/// <summary>
/// An individual message (send instance) within an EmailCampaign.
/// One campaign can have multiple messages (e.g. A/B tests, follow-ups).
/// Status: "draft" | "sending" | "sent" | "failed"
/// </summary>
public class EmailCampaignMessage : TenantEntity
{
    public Guid CampaignId { get; set; }
    public EmailCampaign Campaign { get; set; } = null!;

    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    public string Subject { get; set; } = string.Empty;

    public string HtmlBody { get; set; } = string.Empty;

    /// <summary>When this message was actually sent.</summary>
    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Number of unique email opens tracked.</summary>
    public int OpenCount { get; set; }

    /// <summary>Number of link clicks tracked.</summary>
    public int ClickCount { get; set; }

    /// <summary>Number of recipients this message was sent to.</summary>
    public int RecipientCount { get; set; }

    /// <summary>draft | sending | sent | failed</summary>
    public string Status { get; set; } = "draft";

    /// <summary>Error message if Status == "failed".</summary>
    public string? ErrorMessage { get; set; }
}
