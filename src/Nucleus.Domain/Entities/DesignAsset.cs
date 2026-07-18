namespace Nucleus.Domain.Entities;

/// <summary>
/// A brand asset (image, PDF, SVG, font, etc.) stored in the Asset Library.
/// AssetType: image | document | font | svg | other
/// </summary>
public class DesignAsset : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>Friendly display name for the asset.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Asset category: image | document | font | svg | generated | other.</summary>
    public string AssetType { get; set; } = "image";

    /// <summary>Public URL or storage path where the asset can be accessed.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Width in pixels (images only).</summary>
    public int? Width { get; set; }

    /// <summary>Height in pixels (images only).</summary>
    public int? Height { get; set; }

    /// <summary>File size in bytes.</summary>
    public long? FileSize { get; set; }

    /// <summary>When this asset was uploaded / generated.</summary>
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional AI prompt used to generate the asset (Flux/DALL-E).</summary>
    public string? PromptUsed { get; set; }

    /// <summary>MIME type, e.g. "image/png", "application/pdf".</summary>
    public string? MimeType { get; set; }
}
