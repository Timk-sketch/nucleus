namespace Nucleus.Domain.Entities;

public class BrandProvisioningStep : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public string StepName { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int AttemptCount { get; set; }
}
