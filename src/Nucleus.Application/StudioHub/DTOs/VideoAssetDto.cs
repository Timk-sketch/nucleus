namespace Nucleus.Application.StudioHub.DTOs;

public class VideoAssetDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public int? DurationSeconds { get; set; }
    public string Platform { get; set; } = "other";
    public DateTimeOffset UploadedAt { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
