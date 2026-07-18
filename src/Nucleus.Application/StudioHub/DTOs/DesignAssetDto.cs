namespace Nucleus.Application.StudioHub.DTOs;

public class DesignAssetDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssetType { get; set; } = "image";
    public string Url { get; set; } = string.Empty;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public long? FileSize { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public string? PromptUsed { get; set; }
    public string? MimeType { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class AssetLibraryDto
{
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public int TotalAssets { get; set; }
    public int ImageCount { get; set; }
    public int GeneratedCount { get; set; }
    public List<DesignAssetDto> Assets { get; set; } = [];
}
