namespace Nucleus.Domain.Entities;

/// <summary>
/// A video asset stored in the Video Library.
/// Platform: youtube | vimeo | heygen | cloudflare | local | other
/// </summary>
public class VideoAsset : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>Friendly display name for the video.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Playback or storage URL for the video.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>URL of the thumbnail / poster image.</summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>Video duration in seconds.</summary>
    public int? DurationSeconds { get; set; }

    /// <summary>Source platform: youtube | vimeo | heygen | cloudflare | local | other.</summary>
    public string Platform { get; set; } = "other";

    /// <summary>When this video was uploaded / added.</summary>
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional transcript or description.</summary>
    public string? Description { get; set; }
}
