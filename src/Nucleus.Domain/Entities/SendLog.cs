namespace Nucleus.Domain.Entities;

/// <summary>
/// Immutable audit record for every distribution send event (email blast, social post, etc.).
/// channel: "email" | "social" | "sms"
/// status:  "sent"  | "failed" | "partial"
/// provider: "ghl" | "smtp" | "sendgrid" | "drip" | "vista"
/// </summary>
public class SendLog : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>Optional reference to the EmailCampaign that triggered this send.</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>Optional reference to the SocialPost that was published.</summary>
    public Guid? SocialPostId { get; set; }

    /// <summary>Distribution channel: email | social | sms</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>Number of recipients / accounts reached.</summary>
    public int RecipientCount { get; set; }

    /// <summary>Timestamp when the send was initiated.</summary>
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Provider used for this send: ghl | smtp | sendgrid | drip | vista</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>sent | failed | partial</summary>
    public string Status { get; set; } = "sent";

    /// <summary>Error details when Status is "failed" or "partial".</summary>
    public string? ErrorMessage { get; set; }
}
