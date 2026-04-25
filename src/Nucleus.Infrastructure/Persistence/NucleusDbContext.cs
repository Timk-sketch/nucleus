using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Infrastructure.Persistence;

public class NucleusDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, INucleusDbContext
{
    private readonly ICurrentTenantService? _currentTenant;

    public NucleusDbContext(DbContextOptions<NucleusDbContext> options, ICurrentTenantService? currentTenant = null)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<BrandProvisioningStep> BrandProvisioningSteps => Set<BrandProvisioningStep>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Rename Identity tables for cleanliness
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<ApplicationRole>().ToTable("roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("role_claims");

        // Tenants table
        builder.Entity<Tenant>(t =>
        {
            t.ToTable("tenants");
            t.HasKey(x => x.Id);
            t.Property(x => x.Slug).HasMaxLength(100);
            t.HasIndex(x => x.Slug).IsUnique();
        });

        // Users FK to Tenant
        builder.Entity<ApplicationUser>(u =>
        {
            u.HasOne(x => x.Tenant)
             .WithMany(t => t.Users)
             .HasForeignKey(x => x.TenantId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Brands table
        builder.Entity<Brand>(b =>
        {
            b.ToTable("brands");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).HasMaxLength(20);
            b.Property(x => x.Domain).HasMaxLength(253);
            b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();

            // Encrypted credential columns
            b.Property(x => x.WpAppPassword).HasColumnName("wp_app_password_enc");
            b.Property(x => x.GhlApiKey).HasColumnName("ghl_api_key_enc");
            b.Property(x => x.DripApiToken).HasColumnName("drip_api_token_enc");
            b.Property(x => x.SendgridApiKey).HasColumnName("sendgrid_api_key_enc");
        });

        // BrandProvisioningSteps table
        builder.Entity<BrandProvisioningStep>(s =>
        {
            s.ToTable("brand_provisioning_steps");
            s.HasKey(x => x.Id);
            s.HasOne(x => x.Brand)
             .WithMany(b => b.ProvisioningSteps)
             .HasForeignKey(x => x.BrandId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Auto-apply tenant filter to all TenantEntity subclasses
        ApplyTenantQueryFilters(builder);
    }

    private void ApplyTenantQueryFilters(ModelBuilder builder)
    {
        var tenantId = _currentTenant?.TenantId ?? Guid.Empty;

        builder.Entity<Brand>().HasQueryFilter(b => b.TenantId == tenantId);
        builder.Entity<BrandProvisioningStep>().HasQueryFilter(s => s.TenantId == tenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-set UpdatedAt on all TenantEntity changes
        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
