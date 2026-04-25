using Microsoft.AspNetCore.Identity;

namespace Nucleus.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = "BrandEditor";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
