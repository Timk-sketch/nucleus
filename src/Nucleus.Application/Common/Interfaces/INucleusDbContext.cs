using Microsoft.EntityFrameworkCore;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.Common.Interfaces;

public interface INucleusDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Brand> Brands { get; }
    DbSet<BrandProvisioningStep> BrandProvisioningSteps { get; }
    DbSet<BrandKeyword> BrandKeywords { get; }
    DbSet<GhlContact> GhlContacts { get; }
    DbSet<KeywordRank> KeywordRanks { get; }
    DbSet<KeywordRankSnapshot> KeywordRankSnapshots { get; }
    DbSet<SearchAlert> SearchAlerts { get; }
    DbSet<TopicCluster> TopicClusters { get; }
    DbSet<EmailCampaign> EmailCampaigns { get; }

    // Sprint 24 — Content Hub
    DbSet<ContentPage> ContentPages { get; }
    DbSet<ContentTemplate> ContentTemplates { get; }
    DbSet<AiUsage> AiUsages { get; }
    DbSet<BannedWord> BannedWords { get; }

    // Sprint 26 — Distribution Hub
    DbSet<SocialPost> SocialPosts { get; }
    DbSet<EmailCampaignMessage> EmailCampaignMessages { get; }
    DbSet<SendLog> SendLogs { get; }

    // Sprint 27 — Authority Hub
    DbSet<BacklinkRecord> BacklinkRecords { get; }
    DbSet<BrandMention> BrandMentions { get; }
    DbSet<SchemaTemplate> SchemaTemplates { get; }
    DbSet<OutreachQueueItem> OutreachQueueItems { get; }

    // Sprint 28 — Studio Hub
    DbSet<WebsitePage> WebsitePages { get; }
    DbSet<DesignAsset> DesignAssets { get; }
    DbSet<VideoAsset> VideoAssets { get; }

    // Sprint 29 — CMS Renderer Hub
    DbSet<SiteDomain> SiteDomains { get; }
    DbSet<SiteDeployment> SiteDeployments { get; }
    DbSet<PageCache> PageCaches { get; }
    DbSet<SiteVisit> SiteVisits { get; }

    // Sprint 30 — Finder Hub
    DbSet<Finder> Finders { get; }
    DbSet<FinderStep> FinderSteps { get; }
    DbSet<FinderOption> FinderOptions { get; }
    DbSet<FinderResult> FinderResults { get; }
    DbSet<FinderSession> FinderSessions { get; }
    DbSet<FinderAnalytics> FinderAnalytics { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
