namespace Nucleus.Domain.Entities;

public class BrandProvisioningStep : TenantEntity
{
    public Guid BrandId { get; set; }
    public string StepName { get; set; } = string.Empty;       // wordpress | ghl | dataforseo | backlinks | email
    public string Status { get; set; } = "pending";            // pending | running | success | failed | skipped
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int AttemptCount { get; set; } = 0;

    public Brand? Brand { get; set; }
}
