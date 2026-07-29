namespace Nucleus.Application.ReportsHub.DTOs;

public class SearchReportDto
{
    public int KeywordsTotal { get; set; }
    public int Top10 { get; set; }
    public int Top30 { get; set; }
    public int Top100 { get; set; }
    public int Unranked { get; set; }
    public int TotalSearchVolume { get; set; }

    public List<KeywordMoverRow> TopMovers { get; set; } = [];
    public List<KeywordMoverRow> TopDecliners { get; set; } = [];
}

public class KeywordMoverRow
{
    public string Keyword { get; set; } = "";
    public int? CurrentPosition { get; set; }
    public int? PreviousPosition { get; set; }
    public int Delta { get; set; }       // positive = improved, negative = declined
    public int? SearchVolume { get; set; }
}
