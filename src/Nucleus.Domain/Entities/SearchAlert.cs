namespace Nucleus.Domain.Entities;

/// <summary>
/// Alert rule that fires when a keyword's ranking crosses a threshold.
/// </summary>
public class SearchAlert : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    public Guid KeywordId { get; set; }
    public BrandKeyword Keyword { get; set; } = null!;

    /// <summary>
    /// Type of alert: "rank_drop", "rank_rise", "out_of_top_10", "entered_top_3"
    /// </summary>
    public string AlertType { get; set; } = "rank_drop";

    /// <summary>
    /// Threshold position. E.g. alert when rank drops below 20.
    /// </summary>
    public int Threshold { get; set; }

    /// <summary>When this alert was last triggered. Null = never triggered.</summary>
    public DateTimeOffset? TriggeredAt { get; set; }

    /// <summary>Whether the alert rule is still active (not dismissed).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Human-readable description of what triggered the alert.</summary>
    public string? Message { get; set; }
}
