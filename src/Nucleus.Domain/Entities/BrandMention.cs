namespace Nucleus.Domain.Entities;

/// <summary>
/// A brand mention discovered on the web — could be a forum post, news article, review, etc.
/// Sentiment: "positive" | "neutral" | "negative"
/// </summary>
public class BrandMention : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>The URL where the brand was mentioned.</summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>The snippet or excerpt that contains the mention.</summary>
    public string MentionText { get; set; } = string.Empty;

    /// <summary>Detected sentiment: positive | neutral | negative</summary>
    public string Sentiment { get; set; } = "neutral";

    /// <summary>When this mention was discovered.</summary>
    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Whether a team member has reviewed this mention.</summary>
    public bool IsReviewed { get; set; }
}
