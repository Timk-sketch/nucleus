using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Infrastructure.Data;

public class NucleusDbContext(
    DbContextOptions<NucleusDbContext> options,
    ICurrentTenantService tenantService)
    : IdentityDbContext<ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole<Guid>, Guid>(options), INucleusDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<BrandProvisioningStep> BrandProvisioningSteps => Set<BrandProvisioningStep>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<BrandKeyword> BrandKeywords => Set<BrandKeyword>();
    public DbSet<GhlContact> GhlContacts => Set<GhlContact>();
    public DbSet<KeywordRank> KeywordRanks => Set<KeywordRank>();
    public DbSet<EmailCampaign> EmailCampaigns => Set<EmailCampaign>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).HasMaxLength(200).IsRequired();
            e.Property(t => t.Slug).HasMaxLength(100).IsRequired();
            e.Property(t => t.Plan).HasMaxLength(50).HasDefaultValue("starter");
            e.HasMany(t => t.Users).WithOne(u => u.Tenant).HasForeignKey(u => u.TenantId);
            e.HasMany(t => t.Brands).WithOne().HasForeignKey(b => b.TenantId);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.ToTable("nucleus_users");
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            e.Property(u => u.Role).HasMaxLength(50).HasDefaultValue("BrandEditor");
        });

        var enc = new EncryptedStringConverter();

        builder.Entity<Brand>(e =>
        {
            e.ToTable("brands");
            e.HasKey(b => b.Id);
            e.HasIndex(b => b.TenantId);
            e.Property(b => b.Code).HasMaxLength(20).IsRequired();
            e.Property(b => b.Name).HasMaxLength(200).IsRequired();
            e.Property(b => b.Domain).HasMaxLength(300);
            e.Property(b => b.Slug).HasMaxLength(200);
            e.Property(b => b.Status).HasMaxLength(50).HasDefaultValue("onboarding");
            e.Property(b => b.ServicesProvisioned).HasColumnType("jsonb").HasDefaultValue("{}");
            // Encrypt credentials at rest — decrypted transparently on read
            e.Property(b => b.WpAppPassword).HasConversion(enc);
            e.Property(b => b.GhlApiKey).HasConversion(enc);
            e.Property(b => b.DripApiToken).HasConversion(enc);
            e.Property(b => b.SendgridApiKey).HasConversion(enc);
            e.HasMany(b => b.ProvisioningSteps)
                .WithOne(s => s.Brand)
                .HasForeignKey(s => s.BrandId);
        });

        builder.Entity<BrandKeyword>(e =>
        {
            e.ToTable("brand_keywords");
            e.HasKey(k => k.Id);
            e.HasIndex(k => k.TenantId);
            e.Property(k => k.Keyword).HasMaxLength(300).IsRequired();
            e.Property(k => k.TargetUrl).HasMaxLength(500);
            e.Property(k => k.Notes).HasMaxLength(1000);
            e.HasOne(k => k.Brand).WithMany(b => b.Keywords).HasForeignKey(k => k.BrandId);
            e.HasMany(k => k.Ranks).WithOne(r => r.Keyword).HasForeignKey(r => r.KeywordId);
        });

        builder.Entity<GhlContact>(e =>
        {
            e.ToTable("ghl_contacts");
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.TenantId);
            e.Property(c => c.GhlContactId).HasMaxLength(100).IsRequired();
            e.Property(c => c.FirstName).HasMaxLength(200);
            e.Property(c => c.LastName).HasMaxLength(200);
            e.Property(c => c.Email).HasMaxLength(300);
            e.Property(c => c.Phone).HasMaxLength(50);
            e.HasIndex(c => new { c.BrandId, c.GhlContactId }).IsUnique();
            e.HasOne(c => c.Brand).WithMany().HasForeignKey(c => c.BrandId);
        });

        builder.Entity<KeywordRank>(e =>
        {
            e.ToTable("keyword_ranks");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.TenantId);
            e.HasIndex(r => new { r.KeywordId, r.CheckedAt });
            e.Property(r => r.RankedUrl).HasMaxLength(500);
        });

        builder.Entity<EmailCampaign>(e =>
        {
            e.ToTable("email_campaigns");
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.TenantId);
            e.Property(c => c.Subject).HasMaxLength(500).IsRequired();
            e.Property(c => c.Status).HasMaxLength(50).HasDefaultValue("draft");
            e.HasOne(c => c.Brand).WithMany().HasForeignKey(c => c.BrandId);
        });

        builder.Entity<BrandProvisioningStep>(e =>
        {
            e.ToTable("brand_provisioning_steps");
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.TenantId);
            e.Property(s => s.StepName).HasMaxLength(100).IsRequired();
            e.Property(s => s.Status).HasMaxLength(50).HasDefaultValue("pending");
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("nucleus_user_sessions");
            e.HasKey(r => r.Id);
            e.Property(r => r.Token).HasMaxLength(500).IsRequired();
            e.HasIndex(r => r.Token).IsUnique();
        });

        // Global tenant query filter on all TenantEntity subclasses
        var tenantId = tenantService.TenantId;
        foreach (var entityType in builder.Model.GetEntityTypes()
            .Where(e => typeof(TenantEntity).IsAssignableFrom(e.ClrType)))
        {
            var param = Expression.Parameter(entityType.ClrType, "e");
            var prop = Expression.Property(param, nameof(TenantEntity.TenantId));
            var filter = Expression.Lambda(
                Expression.Equal(prop, Expression.Constant(tenantId)),
                param);
            builder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }

        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>().ToTable("nucleus_roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>().ToTable("nucleus_user_roles");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<Guid>>().ToTable("nucleus_user_claims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<Guid>>().ToTable("nucleus_user_logins");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<Guid>>().ToTable("nucleus_role_claims");
        builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<Guid>>().ToTable("nucleus_user_tokens");
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<TenantEntity>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
