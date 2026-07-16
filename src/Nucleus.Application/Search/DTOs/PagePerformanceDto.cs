namespace Nucleus.Application.Search.DTOs;

public class PagePerformanceDto
{
    public string Url { get; set; } = string.Empty;
    public int KeywordCount { get; set; }
    public double AveragePosition { get; set; }
    public int BestPosition { get; set; }
    public int? TotalSearchVolume { get; set; }
    public List<string> TopKeywords { get; set; } = [];
}
