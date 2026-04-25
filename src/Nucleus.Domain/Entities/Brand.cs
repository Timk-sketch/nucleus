namespace Nucleus.Domain.Entities;

public class Brand : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#6366f1";
    public string? LogoUrl { get; set; }
    public string Status { get; set; } = "onboarding";
    public int OnboardingStep { get; set; }
    public DateTimeOffset? OnboardingCompletedAt { get; set; }
    public string ServicesProvisioned { get; set; } = "{}";

    public string? WpSiteUrl { get; set; }
    public string? WpUsername { get; set; }
    public string? WpAppPassword { get; set; }

    public string? GhlLocationId { get; set; }
    public string? GhlApiKey { get; set; }

    public string? DataForSeoTag { get; set; }

    public string? EmailProvider { get; set; }
    public string? DripAccountId { get; set; }
    public string? DripApiToken { get; set; }
    public string? SendgridApiKey { get; set; }

    public string? IndexNowKey { get; set; }
    public string? GscProperty { get; set; }
    public string? BrandVoice { get; set; }
    public string? PrimaryKeywordsJson { get; set; }
    public string? CompetitorDomainsJson { get; set; }

    public ICollection<BrandProvisioningStep> ProvisioningSteps { get; set; } = [];
}
