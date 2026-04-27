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
    DbSet<EmailCampaign> EmailCampaigns { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
