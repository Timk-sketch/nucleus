namespace Nucleus.Application.Search.DTOs;

public class RankHistoryPointDto
{
    public DateTimeOffset CheckedAt { get; set; }
    public int? Position { get; set; }
    public string? RankedUrl { get; set; }
}

public class RankHistoryDto
{
    public Guid KeywordId { get; set; }
    public string Keyword { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public List<RankHistoryPointDto> History { get; set; } = [];
}
