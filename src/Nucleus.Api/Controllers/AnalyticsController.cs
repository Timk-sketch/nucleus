using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common;
using Nucleus.Infrastructure.Data;
using System.Security.Claims;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
[Authorize]
[Produces("application/json")]
public class AnalyticsController(NucleusDbContext db) : ControllerBase
{
    private Guid CurrentTenantId =>
        Guid.Parse(User.FindFirstValue("tenant_id") ?? Guid.Empty.ToString());

    // GET /api/v1/analytics/overview
    // Returns all dashboard KPIs in a single query batch
    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var tenantId = CurrentTenantId;

        // Brands
        var brands = await db.Brands
            .Where(b => b.TenantId == tenantId)
            .Select(b => new { b.Id, b.Name, b.Status, b.PrimaryColor, b.WpSiteUrl, b.GhlLocationId })
            .ToListAsync(ct);

        var brandIds = brands.Select(b => b.Id).ToList();

        // Keyword counts per brand
        var kwCounts = await db.BrandKeywords
            .Where(k => k.TenantId == tenantId)
            .GroupBy(k => k.BrandId)
            .Select(g => new { BrandId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Provisioning step health per brand
        var stepHealth = await db.BrandProvisioningSteps
            .Where(s => s.TenantId == tenantId)
            .GroupBy(s => s.BrandId)
            .Select(g => new
            {
                BrandId = g.Key,
                Total = g.Count(),
                Completed = g.Count(s => s.Status == "completed"),
                Failed = g.Count(s => s.Status == "failed"),
                Skipped = g.Count(s => s.Status == "skipped"),
            })
            .ToListAsync(ct);

        // Team size
        var userCount = await db.Users
            .CountAsync(u => u.TenantId == tenantId, ct);

        // Total keywords
        var totalKeywords = kwCounts.Sum(k => k.Count);

        // Brand health cards
        var brandCards = brands.Select(b =>
        {
            var kw = kwCounts.FirstOrDefault(k => k.BrandId == b.Id);
            var steps = stepHealth.FirstOrDefault(s => s.BrandId == b.Id);
            var integrations = new List<string>();
            if (!string.IsNullOrEmpty(b.WpSiteUrl)) integrations.Add("WordPress");
            if (!string.IsNullOrEmpty(b.GhlLocationId)) integrations.Add("GHL");

            return new
            {
                b.Id,
                b.Name,
                b.Status,
                b.PrimaryColor,
                Keywords = kw?.Count ?? 0,
                Integrations = integrations,
                Steps = steps == null ? (object?)null : new
                {
                    steps.Total,
                    steps.Completed,
                    steps.Failed,
                    steps.Skipped,
                    HealthPct = steps.Total == 0 ? 0
                        : (int)Math.Round((steps.Completed + steps.Skipped) * 100.0 / steps.Total),
                },
            };
        }).ToList();

        // Recent activity — last 10 brands or keywords added
        var recentBrands = await db.Brands
            .Where(b => b.TenantId == tenantId)
            .OrderByDescending(b => b.CreatedAt)
            .Take(5)
            .Select(b => new { b.Name, b.Status, b.CreatedAt, Type = "brand" })
            .ToListAsync(ct);

        var recentKeywords = await db.BrandKeywords
            .Where(k => k.TenantId == tenantId)
            .OrderByDescending(k => k.CreatedAt)
            .Take(5)
            .Select(k => new { k.Keyword, k.CreatedAt, Type = "keyword" })
            .ToListAsync(ct);

        var activity = recentBrands
            .Select(b => new { b.Type, Label = b.Name, Meta = b.Status, b.CreatedAt })
            .Concat(recentKeywords.Select(k => new { k.Type, Label = k.Keyword, Meta = (string?)"keyword", k.CreatedAt }))
            .OrderByDescending(a => a.CreatedAt)
            .Take(8)
            .ToList();

        return Ok(ApiResponse.Ok(new
        {
            Summary = new
            {
                TotalBrands = brands.Count,
                ActiveBrands = brands.Count(b => b.Status == "active"),
                OnboardingBrands = brands.Count(b => b.Status == "onboarding"),
                TotalKeywords = totalKeywords,
                TeamSize = userCount,
            },
            Brands = brandCards,
            RecentActivity = activity,
        }));
    }
}
