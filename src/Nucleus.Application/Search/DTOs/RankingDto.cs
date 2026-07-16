namespace Nucleus.Application.Search.DTOs;

public class RankingDto
{
    public Guid KeywordId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public int? CurrentPosition { get; set; }
    public int? PreviousPosition { get; set; }
    public int? PositionDelta { get; set; }
    public string? RankedUrl { get; set; }
    public int? SearchVolume { get; set; }
    public DateTimeOffset? LastChecked { get; set; }
}

public class RankingsDashboardDto
{
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public int TotalKeywords { get; set; }
    public int RankingKeywords { get; set; }
    public int Top3Keywords { get; set; }
    public int Top10Keywords { get; set; }
    public int Top30Keywords { get; set; }
    public List<RankingDto> Keywords { get; set; } = [];
}
