namespace Nucleus.Application.Distribution.DTOs;

public class SocialPostDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public Guid? ContentPageId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ExternalPostId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Provider { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
