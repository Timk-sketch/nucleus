namespace Nucleus.Application.CmsRendererHub.DTOs;

/// <summary>Analytics summary for a brand's CMS site.</summary>
public class SiteAnalyticsDto
{
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;

    /// <summary>Total visits in the selected time window.</summary>
    public int TotalVisits { get; set; }

    /// <summary>Unique pages visited (distinct slugs) in the time window.</summary>
    public int UniquePages { get; set; }

    /// <summary>Top pages by visit count.</summary>
    public List<PageVisitSummary> TopPages { get; set; } = [];

    /// <summary>Daily visit counts for the time window (for a sparkline chart).</summary>
    public List<DailyVisitCount> DailyVisits { get; set; } = [];
}

public class PageVisitSummary
{
    public string Slug { get; set; } = string.Empty;
    public int Visits { get; set; }
}

public class DailyVisitCount
{
    public DateOnly Date { get; set; }
    public int Visits { get; set; }
}
