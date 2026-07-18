namespace Nucleus.Domain.Entities;

/// <summary>
/// A single step (question screen) within a Finder quiz.
/// Steps are ordered by StepOrder and may be required.
/// StepType: single_choice | multi_choice | text | date | number
/// </summary>
public class FinderStep : TenantEntity
{
    public Guid FinderId { get; set; }
    public Finder Finder { get; set; } = null!;

    /// <summary>1-based display order within the finder.</summary>
    public int StepOrder { get; set; }

    /// <summary>Step type: single_choice | multi_choice | text | date | number.</summary>
    public string StepType { get; set; } = "single_choice";

    /// <summary>The question text displayed to the user.</summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>Optional helper/subtitle text shown below the question.</summary>
    public string? HelperText { get; set; }

    /// <summary>Whether the user must answer this step before continuing.</summary>
    public bool IsRequired { get; set; } = true;

    // Navigation
    public ICollection<FinderOption> Options { get; set; } = new List<FinderOption>();
}
