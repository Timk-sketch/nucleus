namespace Nucleus.Application.StudioHub.DTOs;

/// <summary>
/// Context data for the Design Studio AI builder.
/// Provides brand identity (colors, fonts, voice) + recent pages/assets
/// so the AI can generate on-brand content.
/// </summary>
public class DesignStudioContextDto
{
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? PrimaryColor { get; set; }

    /// <summary>Recent published pages for context.</summary>
    public List<PageSummary> RecentPages { get; set; } = [];

    /// <summary>Recent design assets for visual context.</summary>
    public List<AssetSummary> RecentAssets { get; set; } = [];

    /// <summary>Statistics for the studio dashboard.</summary>
    public StudioStats Stats { get; set; } = new();
}

public class PageSummary
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PageType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class AssetSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class StudioStats
{
    public int TotalPages { get; set; }
    public int PublishedPages { get; set; }
    public int TotalAssets { get; set; }
    public int GeneratedImages { get; set; }
    public int TotalVideos { get; set; }
}
