namespace Nucleus.Application.ReportsHub.DTOs;

public class ContentReportDto
{
    public int DaysWindow { get; set; }

    // Status breakdown
    public int TotalPages { get; set; }
    public int Draft { get; set; }
    public int InReview { get; set; }
    public int Approved { get; set; }
    public int Published { get; set; }
    public int Rejected { get; set; }

    // Page type breakdown
    public List<ContentTypeRow> ByPageType { get; set; } = [];

    // AI cost breakdown
    public decimal TotalAiSpendUsd { get; set; }
    public int TotalAiTokens { get; set; }
    public List<AiFeatureRow> AiByFeature { get; set; } = [];
}

public class ContentTypeRow
{
    public string PageType { get; set; } = "";
    public int Count { get; set; }
    public int Published { get; set; }
}

public class AiFeatureRow
{
    public string Feature { get; set; } = "";
    public string Model { get; set; } = "";
    public int Tokens { get; set; }
    public decimal CostUsd { get; set; }
}
