namespace Nucleus.Domain.Entities;

/// <summary>
/// Tracks every AI generation call for cost metering and plan enforcement.
/// Written after every successful AI generation. Checked before generating to enforce plan limits.
/// </summary>
public class AiUsage : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>Feature that triggered this call: "content_generation" | "design_studio" | "image_gen" etc.</summary>
    public string Feature { get; set; } = string.Empty;

    public int TokensUsed { get; set; }

    public decimal CostUsd { get; set; }

    /// <summary>AI model used: "claude-3-5-sonnet" | "claude-3-haiku" etc.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>Optional reference to the ContentPage this generation produced.</summary>
    public Guid? ContentPageId { get; set; }
}
