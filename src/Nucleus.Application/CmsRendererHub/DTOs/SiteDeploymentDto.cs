namespace Nucleus.Application.CmsRendererHub.DTOs;

/// <summary>DTO for a site deployment event.</summary>
public class SiteDeploymentDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string BrandName { get; set; } = string.Empty;
    public Guid DeployedBy { get; set; }
    public int PageCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? DeployedAt { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
