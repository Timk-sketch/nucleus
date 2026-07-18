namespace Nucleus.Domain.Entities;

/// <summary>
/// A selectable answer option for a FinderStep.
/// Options have a display label and an internal value used in result condition matching.
/// </summary>
public class FinderOption : TenantEntity
{
    public Guid StepId { get; set; }
    public FinderStep Step { get; set; } = null!;

    /// <summary>Display label shown to the user (e.g. "Car", "Motorcycle").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Internal value used for condition matching (e.g. "car", "moto").</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Optional icon/image URL displayed alongside the label.</summary>
    public string? IconUrl { get; set; }

    /// <summary>Optional longer description shown below the label.</summary>
    public string? Description { get; set; }

    /// <summary>Display sort order within the step (0-based).</summary>
    public int SortOrder { get; set; }
}
