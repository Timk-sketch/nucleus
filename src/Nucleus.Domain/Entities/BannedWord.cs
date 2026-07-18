namespace Nucleus.Domain.Entities;

/// <summary>
/// Brand Voice: words or phrases the AI generator and content library must never use.
/// Scoped per brand within a tenant. Checked at generation time before content is accepted.
/// </summary>
public class BannedWord : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    public string Word { get; set; } = string.Empty;

    /// <summary>Why this word/phrase is banned — shown in the Brand Voice editor.</summary>
    public string? Reason { get; set; }
}
