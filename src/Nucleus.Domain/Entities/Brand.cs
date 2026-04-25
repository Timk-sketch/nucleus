namespace Nucleus.Domain.Entities;

public class Brand : TenantEntity
{
    public string Code { get; set; } = string.Empty;           // 'dl', 'rl', 'mrs'
    public string Name { get; set; } = string.Empty;           // 'Dirt Legal'
    public string Domain { get; set; } = string.Empty;         // 'dirtlegal.com'
    public string Slug { get; set; } = string.Empty;           // 'dirt-legal'
    public string PrimaryColor { get; set; } = "#6366f1";
    public string? LogoUrl { get; set; }
    public string Status { get; set; } = "onboarding";         // active | inactive | onboarding
    public int OnboardingStep { get; set; } = 0;
    public DateTimeOffset? OnboardingCompletedAt { get; set; }

    // services_provisioned: {"wordpress": true, "ghl": false, "dataforseo": false}
    public string ServicesProvisionedJson { get; set; } = "{}";

    // WordPress credentials (encrypted at application layer)
    public string? WpSiteUrl { get; set; }
    public string? WpUsername { get; set; }
    public string? WpAppPassword { get; set; }

    // GHL credentials (encrypted at application layer)
    public string? GhlLocationId { get; set; }
    public string? GhlApiKey { get; set; }

    // DataForSEO
    public string? DataForSeoTag { get; set; }
    public string? IndexNowKey { get; set; }
    public string? GscProperty { get; set; }

    // Email provider
    public string EmailProvider { get; set; } = "drip";        // drip | sendgrid
    public string? DripAccountId { get; set; }
    public string? DripApiToken { get; set; }
    public string? SendgridApiKey { get; set; }
    public string? SendgridListId { get; set; }

    // Brand intelligence
    public string? BrandVoice { get; set; }
    public string PrimaryKeywordsJson { get; set; } = "[]";
    public string CompetitorDomainsJson { get; set; } = "[]";

    public ICollection<BrandProvisioningStep> ProvisioningSteps { get; set; } = new List<BrandProvisioningStep>();
}
