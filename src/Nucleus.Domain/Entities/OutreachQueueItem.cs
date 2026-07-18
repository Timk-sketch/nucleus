namespace Nucleus.Domain.Entities;

/// <summary>
/// An outreach prospect in the link-building / PR queue.
/// Status: "pending" | "emailed" | "replied" | "accepted" | "rejected" | "skipped"
/// </summary>
public class OutreachQueueItem : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>The target website or page we want a link from.</summary>
    public string TargetUrl { get; set; } = string.Empty;

    /// <summary>Contact email for outreach.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>pending | emailed | replied | accepted | rejected | skipped</summary>
    public string Status { get; set; } = "pending";

    /// <summary>Free-text notes about this prospect (pitch angle, relationship, history).</summary>
    public string? Notes { get; set; }

    /// <summary>When the outreach email was sent (null if not yet sent).</summary>
    public DateTimeOffset? OutreachAt { get; set; }
}
