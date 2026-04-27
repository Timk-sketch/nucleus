namespace Nucleus.Domain.Entities;

public class BrandKeyword : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public string Keyword { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public string? Notes { get; set; }
}
