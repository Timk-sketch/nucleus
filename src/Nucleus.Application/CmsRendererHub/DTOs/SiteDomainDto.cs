namespace Nucleus.Application.CmsRendererHub.DTOs;

/// <summary>DTO for a custom domain mapped to a brand's CMS site.</summary>
public class SiteDomainDto
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public bool SslVerified { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
