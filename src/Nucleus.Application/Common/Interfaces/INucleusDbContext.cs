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

    // Sprint 26 — Distribution Hub
    DbSet<SocialPost> SocialPosts { get; }
    DbSet<EmailCampaignMessage> EmailCampaignMessages { get; }
    DbSet<SendLog> SendLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
