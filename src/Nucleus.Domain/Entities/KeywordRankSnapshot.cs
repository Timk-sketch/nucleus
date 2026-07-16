namespace Nucleus.Domain.Entities;

/// <summary>
/// Point-in-time snapshot of a keyword's search ranking position.
/// Stored separately from KeywordRank to support historical charting.
/// </summary>
public class KeywordRankSnapshot : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    public Guid KeywordId { get; set; }
    public BrandKeyword Keyword { get; set; } = null!;

    /// <summary>SERP position (1-100+). Null = not ranking.</summary>
    public int? Position { get; set; }

    /// <summary>The URL that ranked for the keyword.</summary>
    public string? Url { get; set; }

    /// <summary>Monthly search volume from DataForSEO.</summary>
    public int? SearchVolume { get; set; }

    /// <summary>Competition score 0-1 from DataForSEO.</summary>
    public decimal? Competition { get; set; }

    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
}
