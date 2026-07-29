namespace Nucleus.Application.Common.Interfaces;

public interface ITenantPlanService
{
    /// <summary>Current tenant's plan slug (starter / pro / agency).</summary>
    Task<string> GetPlanAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns true if the current tenant is allowed to use the feature.
    /// Checks monthly usage caps for starter plan.
    /// </summary>
    Task<bool> IsFeatureAllowedAsync(string feature, CancellationToken ct = default);

    /// <summary>Current month usage count for a feature.</summary>
    Task<int> GetMonthlyUsageAsync(string feature, CancellationToken ct = default);
}
