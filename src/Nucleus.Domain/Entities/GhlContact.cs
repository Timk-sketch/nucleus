namespace Nucleus.Domain.Entities;

public class GhlContact : TenantEntity
{
    public Guid BrandId { get; set; }
    public Brand Brand { get; set; } = null!;
    public string GhlContactId { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Tags { get; set; } // JSON array from GHL
    public DateTimeOffset? GhlCreatedAt { get; set; }
    public DateTimeOffset SyncedAt { get; set; } = DateTimeOffset.UtcNow;
}
