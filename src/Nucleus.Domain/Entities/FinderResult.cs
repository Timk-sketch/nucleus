namespace Nucleus.Domain.Entities;

/// <summary>
/// A result shown to the user when their answers match the condition.
/// ConditionJson is a JSON object describing the rule, e.g.:
/// { "step1": "car", "step2": "new" }  (all keys must match)
/// or with an array: { "step1": ["car", "truck"] } (any value matches)
/// </summary>
public class FinderResult : TenantEntity
{
    public Guid FinderId { get; set; }
    public Finder Finder { get; set; } = null!;

    /// <summary>
    /// JSON matching rule. Example: {"1": "car", "2": "new"} where keys are StepOrder values
    /// and values are the option Values that must match.
    /// Stored as jsonb.
    /// </summary>
    public string ConditionJson { get; set; } = "{}";

    /// <summary>Internal product/service identifier (e.g. "car-title-transfer").</summary>
    public string ProductKey { get; set; } = string.Empty;

    /// <summary>Result headline displayed to the user.</summary>
    public string Headline { get; set; } = string.Empty;

    /// <summary>Result body copy / description shown to the user.</summary>
    public string? Body { get; set; }

    /// <summary>Call-to-action button label (e.g. "Get Started").</summary>
    public string? CtaLabel { get; set; }

    /// <summary>Call-to-action URL the CTA button points to.</summary>
    public string? CtaUrl { get; set; }
}
