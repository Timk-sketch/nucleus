namespace Nucleus.Domain.Entities;

/// <summary>
/// Records a CMS site deployment event — snapshot of all published WebsitePages
/// for a brand warmed into PageCache.
/// Status: pending | running | complete | failed
/// </summary>
public class SiteDeployment : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;

    /// <summary>UserId of the user who triggered the deploy.</summary>
    public Guid DeployedBy { get; set; }

    /// <summary>Number of pages included in this deployment.</summary>
    public int PageCount { get; set; }

    /// <summary>Deployment lifecycle status: pending | running | complete | failed.</summary>
    public string Status { get; set; } = "pending";

    /// <summary>When the deployment completed (null if still running or failed).</summary>
    public DateTimeOffset? DeployedAt { get; set; }

    /// <summary>Optional notes or error message describing the result.</summary>
    public string? Notes { get; set; }
}
