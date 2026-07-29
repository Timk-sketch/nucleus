namespace Nucleus.Domain.Entities;

/// <summary>
/// Records a single user's interaction session with an embedded Finder.
/// Tracks the user's answers, the matched result, and conversion status.
/// Sessions are keyed by a random session_token (no user login required).
/// </summary>
public class FinderSession : TenantEntity
{
    public Guid FinderId { get; set; }
    public Finder Finder { get; set; } = null!;

    /// <summary>Random UUID token identifying this anonymous session.</summary>
    public string SessionToken { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// JSON object recording the user's answers keyed by StepOrder.
    /// Example: {"1": "car", "2": "new", "3": "colorado"}
    /// </summary>
    public string AnswersJson { get; set; } = "{}";

    /// <summary>ProductKey of the matched FinderResult (null until completed).</summary>
    public string? ResultKey { get; set; }

    /// <summary>Whether the user clicked the CTA (converted to a lead).</summary>
    public bool Converted { get; set; }

    /// <summary>When the user completed all steps and saw a result (null if abandoned).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    // ── Sprint 32: Lead capture + A/B variant ─────────────────────────────

    /// <summary>Lead name captured via lead_capture step (null if not captured).</summary>
    public string? LeadName { get; set; }

    /// <summary>Lead email captured via lead_capture step.</summary>
    public string? LeadEmail { get; set; }

    /// <summary>Lead phone captured via lead_capture step.</summary>
    public string? LeadPhone { get; set; }

    /// <summary>A/B variant assigned to this session (null = no variant / control).</summary>
    public Guid? VariantId { get; set; }
    public FinderVariant? Variant { get; set; }
}
