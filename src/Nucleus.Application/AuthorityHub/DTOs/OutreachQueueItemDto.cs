namespace Nucleus.Application.AuthorityHub.DTOs;

public class OutreachQueueItemDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string Status { get; set; } = "pending";
    public string? Notes { get; set; }
    public DateTimeOffset? OutreachAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
