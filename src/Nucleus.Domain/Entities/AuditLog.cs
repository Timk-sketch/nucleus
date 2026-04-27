namespace Nucleus.Domain.Entities;

/// <summary>
/// Immutable audit record — one row per write operation.
/// Not a TenantEntity so SuperAdmin can query across tenants.
/// </summary>
public class AuditLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TenantId { get; init; }
    public Guid? UserId { get; init; }
    public string Action { get; init; } = default!;       // created | updated | deleted
    public string EntityType { get; init; } = default!;   // Brand | EmailCampaign | etc.
    public string? EntityId { get; init; }
    public string? Changes { get; init; }                 // JSON snapshot (optional)
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
