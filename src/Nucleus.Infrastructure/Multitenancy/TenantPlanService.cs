using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Infrastructure.Data;

namespace Nucleus.Infrastructure.Multitenancy;

/// <summary>
/// Enforces plan-based feature gates.
///
/// Plan limits (per calendar month):
///   starter  — content_generation: 5, design_generation: 3, image_generation: 0 (blocked)
///   pro      — all AI features: unlimited
///   agency   — all AI features: unlimited
/// </summary>
public class TenantPlanService(NucleusDbContext db, ICurrentTenantService tenant) : ITenantPlanService
{
    // Feature → max monthly uses per plan (null = unlimited, 0 = blocked)
    private static readonly Dictionary<string, Dictionary<string, int?>> Limits = new()
    {
        ["content_generation"] = new() { ["starter"] = 5,  ["pro"] = null, ["agency"] = null },
        ["design_generation"]  = new() { ["starter"] = 3,  ["pro"] = null, ["agency"] = null },
        ["image_generation"]   = new() { ["starter"] = 0,  ["pro"] = 0,    ["agency"] = null },
    };

    public async Task<string> GetPlanAsync(CancellationToken ct = default)
    {
        var plan = await db.Tenants
            .Where(t => t.Id == tenant.TenantId)
            .Select(t => t.Plan)
            .FirstOrDefaultAsync(ct);

        return plan ?? "starter";
    }

    public async Task<bool> IsFeatureAllowedAsync(string feature, CancellationToken ct = default)
    {
        var plan = await GetPlanAsync(ct);

        if (!Limits.TryGetValue(feature, out var planLimits))
            return true; // unknown feature = allow

        if (!planLimits.TryGetValue(plan, out var limit))
            return true; // unknown plan = allow

        if (limit == null)
            return true; // unlimited

        if (limit == 0)
            return false; // blocked for this plan

        var usage = await GetMonthlyUsageAsync(feature, ct);
        return usage < limit;
    }

    public async Task<int> GetMonthlyUsageAsync(string feature, CancellationToken ct = default)
    {
        var monthStart = new DateTimeOffset(
            DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

        return await db.AiUsages
            .CountAsync(u => u.TenantId == tenant.TenantId
                          && u.Feature == feature
                          && u.CreatedAt >= monthStart, ct);
    }
}
