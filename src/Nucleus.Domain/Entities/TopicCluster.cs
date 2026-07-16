namespace Nucleus.Domain.Entities;

/// <summary>
/// A topic cluster groups a pillar keyword with its supporting cluster keywords.
/// Used for topical authority planning and content gap analysis.
/// </summary>
public class TopicCluster : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    /// <summary>The main/pillar keyword this cluster is built around.</summary>
    public string PillarKeyword { get; set; } = string.Empty;

    /// <summary>JSON array of supporting cluster keyword strings.</summary>
    public string ClusterKeywordsJson { get; set; } = "[]";

    /// <summary>Status: "planning", "in_progress", "complete"</summary>
    public string Status { get; set; } = "planning";

    /// <summary>Optional notes about the cluster strategy.</summary>
    public string? Notes { get; set; }
}
