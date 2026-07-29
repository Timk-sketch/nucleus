namespace Nucleus.Application.ReportsHub.DTOs;

public class BrandOverviewDto
{
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = "";
    public int DaysWindow { get; set; }

    // Content
    public int ContentPublished { get; set; }
    public int ContentInReview { get; set; }
    public int ContentDraft { get; set; }

    // SEO
    public int KeywordsTop10 { get; set; }
    public int KeywordsTop30 { get; set; }
    public int KeywordsTotal { get; set; }

    // Finders
    public int FinderStarts { get; set; }
    public int FinderCompletions { get; set; }
    public int FinderConversions { get; set; }
    public double? FinderConversionRate { get; set; }

    // AI Spend
    public decimal AiSpendUsd { get; set; }
    public int AiTokensUsed { get; set; }

    // Distribution
    public int EmailReach { get; set; }
    public int SocialPostsPublished { get; set; }
}
