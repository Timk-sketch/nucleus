namespace Nucleus.Application.CmsRendererHub.DTOs;

/// <summary>Current deploy + cache status for a brand's CMS site.</summary>
public class SiteStatusDto
{
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;

    /// <summary>Total number of published WebsitePages for the brand.</summary>
    public int PublishedPageCount { get; set; }

    /// <summary>Number of pages currently cached in PageCache.</summary>
    public int CachedPageCount { get; set; }

    /// <summary>Most recent deployment for this brand (null if never deployed).</summary>
    public SiteDeploymentDto? LastDeployment { get; set; }

    /// <summary>All deployments (most recent first, capped at 20).</summary>
    public List<SiteDeploymentDto> DeployHistory { get; set; } = [];
}
