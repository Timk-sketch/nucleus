namespace Nucleus.Domain.Entities;

/// <summary>
/// Daily aggregate analytics for a Finder widget.
/// Tracks starts, completions, and conversions per day for funnel analysis.
/// One row per Finder per calendar date.
/// </summary>
public class FinderAnalytics : TenantEntity
{
    public Guid FinderId { get; set; }
    public Finder Finder { get; set; } = null!;

    /// <summary>Calendar date for this aggregate row (UTC date, time component always 00:00:00).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Number of sessions started (first step viewed) on this date.</summary>
    public int Starts { get; set; }

    /// <summary>Number of sessions that reached a result on this date.</summary>
    public int Completions { get; set; }

    /// <summary>Number of sessions where the user clicked the CTA (converted) on this date.</summary>
    public int Conversions { get; set; }

    /// <summary>
    /// The FinderStep Id where users most commonly abandoned on this date.
    /// Null if no drop-off data is available.
    /// </summary>
    public Guid? DropOffStepId { get; set; }
    public FinderStep? DropOffStep { get; set; }
}
