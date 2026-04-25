using Microsoft.AspNetCore.Identity;

namespace Nucleus.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";         // SuperAdmin | TenantAdmin | BrandEditor | Viewer
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Tenant? Tenant { get; set; }
}
