namespace Nucleus.Application.AuthorityHub.DTOs;

public class BacklinkRecordDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public string? AnchorText { get; set; }
    public decimal? DomainRating { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public bool IsActive { get; set; }
}

public class BacklinkProfileDto
{
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public int TotalBacklinks { get; set; }
    public int ActiveBacklinks { get; set; }
    public int LostBacklinks { get; set; }
    public int NewLast30Days { get; set; }
    public decimal? AverageDomainRating { get; set; }
    public List<BacklinkRecordDto> Backlinks { get; set; } = [];
}
