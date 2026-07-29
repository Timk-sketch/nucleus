namespace Nucleus.Domain.Entities;

/// <summary>
/// An A/B test variant for a Finder. Each variant can override the intro text.
/// Weight controls traffic allocation (0–100); proportional weighted random assignment.
/// Requires agency plan.
/// </summary>
public class FinderVariant : TenantEntity
{
    public Guid FinderId { get; set; }
    public Finder Finder { get; set; } = null!;

    /// <summary>Variant label shown in analytics (e.g. "Variant A", "Variant B").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Overrides Finder.IntroText for users assigned this variant. Null = use default.</summary>
    public string? IntroTextOverride { get; set; }

    /// <summary>Traffic weight (0–100). Proportional across all variants for a Finder.</summary>
    public int Weight { get; set; } = 50;
}
