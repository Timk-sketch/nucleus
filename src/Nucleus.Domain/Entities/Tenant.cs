namespace Nucleus.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Plan { get; set; } = "starter";
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? SubscriptionStatus { get; set; } // active | trialing | past_due | canceled
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ApplicationUser> Users { get; set; } = [];
    public ICollection<Brand> Brands { get; set; } = [];
}
