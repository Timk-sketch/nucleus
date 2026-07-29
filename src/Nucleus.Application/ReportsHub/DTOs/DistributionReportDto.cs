namespace Nucleus.Application.ReportsHub.DTOs;

public class DistributionReportDto
{
    public int DaysWindow { get; set; }

    // Email
    public int EmailMessagesSent { get; set; }
    public int EmailTotalRecipients { get; set; }
    public int EmailTotalOpens { get; set; }
    public int EmailTotalClicks { get; set; }
    public double? EmailOpenRate { get; set; }
    public double? EmailClickRate { get; set; }

    // Social
    public int SocialPostsPublished { get; set; }
    public int SocialPostsFailed { get; set; }
    public List<SocialPlatformRow> ByPlatform { get; set; } = [];

    // Channel reach
    public int TotalEmailReach { get; set; }
    public int TotalSocialPosts { get; set; }
}

public class SocialPlatformRow
{
    public string Platform { get; set; } = "";
    public int Published { get; set; }
    public int Scheduled { get; set; }
    public int Failed { get; set; }
}
