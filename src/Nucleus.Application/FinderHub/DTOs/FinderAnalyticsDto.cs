namespace Nucleus.Application.FinderHub.DTOs;

/// <summary>Analytics aggregate for a Finder over a date range.</summary>
public class FinderAnalyticsDto
{
    public Guid FinderId { get; set; }
    public string FinderName { get; set; } = string.Empty;

    // Totals over the requested window
    public int TotalStarts { get; set; }
    public int TotalCompletions { get; set; }
    public int TotalConversions { get; set; }

    /// <summary>Completion rate = completions / starts (0–1, null if no starts).</summary>
    public double? CompletionRate { get; set; }

    /// <summary>Conversion rate = conversions / completions (0–1, null if no completions).</summary>
    public double? ConversionRate { get; set; }

    /// <summary>Daily breakdown rows, ascending by date.</summary>
    public List<DailyFinderStats> DailyStats { get; set; } = [];
}

public class DailyFinderStats
{
    public DateOnly Date { get; set; }
    public int Starts { get; set; }
    public int Completions { get; set; }
    public int Conversions { get; set; }
}
