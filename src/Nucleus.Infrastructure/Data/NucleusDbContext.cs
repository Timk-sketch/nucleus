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
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<BrandProvisioningStep> BrandProvisioningSteps => Set<BrandProvisioningStep>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<BrandKeyword> BrandKeywords => Set<BrandKeyword>();
    public DbSet<GhlContact> GhlContacts => Set<GhlContact>();
    public DbSet<KeywordRank> KeywordRanks => Set<KeywordRank>();
    public DbSet<KeywordRankSnapshot> KeywordRankSnapshots => Set<KeywordRankSnapshot>();
    public DbSet<SearchAlert> SearchAlerts => Set<SearchAlert>();
    public DbSet<TopicCluster> TopicClusters => Set<TopicCluster>();
    public DbSet<EmailCampaign> EmailCampaigns => Set<EmailCampaign>();

    // Sprint 26 — Distribution Hub
    public DbSet<SocialPost> SocialPosts => Set<SocialPost>();
    public DbSet<EmailCampaignMessage> EmailCampaignMessages => Set<EmailCampaignMessage>();
    public DbSet<SendLog> SendLogs => Set<SendLog>();

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
            e.Property(t => t.StripeCustomerId).HasMaxLength(100);
            e.Property(t => t.StripeSubscriptionId).HasMaxLength(100);
            e.Property(t => t.SubscriptionStatus).HasMaxLength(50);
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

        // ── Sprint 25: Search Hub entities ────────────────────────────────

        builder.Entity<KeywordRankSnapshot>(e =>
        {
            e.ToTable("keyword_rank_snapshots");
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.TenantId);
            e.HasIndex(s => new { s.TenantId, s.BrandId });
            e.HasIndex(s => new { s.KeywordId, s.CheckedAt });
            e.Property(s => s.Url).HasMaxLength(500);
            e.Property(s => s.Competition).HasColumnType("decimal(5,4)");
            e.HasOne(s => s.Brand).WithMany().HasForeignKey(s => s.BrandId);
            e.HasOne(s => s.Keyword).WithMany().HasForeignKey(s => s.KeywordId);
        });

        builder.Entity<SearchAlert>(e =>
        {
            e.ToTable("search_alerts");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.TenantId);
            e.HasIndex(a => new { a.TenantId, a.BrandId });
            e.HasIndex(a => new { a.KeywordId, a.IsActive });
            e.Property(a => a.AlertType).HasMaxLength(50).IsRequired();
            e.Property(a => a.Message).HasMaxLength(500);
            e.HasOne(a => a.Brand).WithMany().HasForeignKey(a => a.BrandId);
            e.HasOne(a => a.Keyword).WithMany().HasForeignKey(a => a.KeywordId);
        });

        builder.Entity<TopicCluster>(e =>
        {
            e.ToTable("topic_clusters");
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.TenantId);
            e.HasIndex(c => new { c.TenantId, c.BrandId });
            e.Property(c => c.Name).HasMaxLength(200).IsRequired();
            e.Property(c => c.PillarKeyword).HasMaxLength(300).IsRequired();
            e.Property(c => c.ClusterKeywordsJson).HasColumnType("jsonb").HasDefaultValue("[]");
            e.Property(c => c.Status).HasMaxLength(50).HasDefaultValue("planning");
            e.Property(c => c.Notes).HasMaxLength(1000);
            e.HasOne(c => c.Brand).WithMany().HasForeignKey(c => c.BrandId);
        });

        // ── End Sprint 25 ─────────────────────────────────────────────────

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

        builder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.TenantId);
            e.HasIndex(a => a.CreatedAt);
            e.Property(a => a.Action).HasMaxLength(50).IsRequired();
            e.Property(a => a.EntityType).HasMaxLength(100).IsRequired();
            e.Property(a => a.EntityId).HasMaxLength(100);
            e.Property(a => a.Changes).HasColumnType("jsonb");
        });

        // ── Sprint 26: Distribution Hub entities ──────────────────────────

        builder.Entity<SocialPost>(e =>
        {
            e.ToTable("social_posts");
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.TenantId);
            e.HasIndex(p => new { p.TenantId, p.BrandId });
            e.HasIndex(p => new { p.BrandId, p.ScheduledAt });
            e.Property(p => p.Platform).HasMaxLength(50).IsRequired();
            e.Property(p => p.Caption).HasMaxLength(2000).IsRequired();
            e.Property(p => p.ImageUrl).HasMaxLength(500);
            e.Property(p => p.Status).HasMaxLength(50).HasDefaultValue("draft");
            e.Property(p => p.ExternalPostId).HasMaxLength(200);
            e.Property(p => p.ErrorMessage).HasMaxLength(500);
            e.Property(p => p.Provider).HasMaxLength(50);
            e.HasOne(p => p.Brand).WithMany().HasForeignKey(p => p.BrandId);
        });

        builder.Entity<EmailCampaignMessage>(e =>
        {
            e.ToTable("email_campaign_messages");
            e.HasKey(m => m.Id);
            e.HasIndex(m => m.TenantId);
            e.HasIndex(m => new { m.TenantId, m.BrandId });
            e.HasIndex(m => m.CampaignId);
            e.Property(m => m.Subject).HasMaxLength(500).IsRequired();
            e.Property(m => m.Status).HasMaxLength(50).HasDefaultValue("draft");
            e.Property(m => m.ErrorMessage).HasMaxLength(500);
            e.HasOne(m => m.Campaign).WithMany().HasForeignKey(m => m.CampaignId);
            e.HasOne(m => m.Brand).WithMany().HasForeignKey(m => m.BrandId);
        });

        builder.Entity<SendLog>(e =>
        {
            e.ToTable("send_logs");
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.TenantId);
            e.HasIndex(l => new { l.TenantId, l.BrandId });
            e.HasIndex(l => new { l.BrandId, l.SentAt });
            e.Property(l => l.Channel).HasMaxLength(50).IsRequired();
            e.Property(l => l.Provider).HasMaxLength(50).IsRequired();
            e.Property(l => l.Status).HasMaxLength(50).HasDefaultValue("sent");
            e.Property(l => l.ErrorMessage).HasMaxLength(1000);
            e.HasOne(l => l.Brand).WithMany().HasForeignKey(l => l.BrandId);
        });

        // ── End Sprint 26 ─────────────────────────────────────────────────

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
