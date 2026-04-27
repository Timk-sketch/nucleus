using System.Net.Http.Headers;
using System.Text;
using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Api.Hubs;
using Nucleus.Domain.Entities;
using Nucleus.Infrastructure.Data;

namespace Nucleus.Api.Jobs;

/// <summary>
/// Verifies each brand integration step against the real external API.
/// Run as a Hangfire background job — no HTTP context, so IgnoreQueryFilters() is required.
/// </summary>
public class BrandProvisioningJob
{
    private readonly NucleusDbContext _db;
    private readonly IHubContext<ProvisioningHub> _hub;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<BrandProvisioningJob> _logger;

    public BrandProvisioningJob(
        NucleusDbContext db,
        IHubContext<ProvisioningHub> hub,
        IHttpClientFactory httpFactory,
        ILogger<BrandProvisioningJob> logger)
    {
        _db = db;
        _hub = hub;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task RunAsync(Guid brandId)
    {
        _logger.LogInformation("Provisioning brand {BrandId}", brandId);

        var brand = await _db.Brands
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == brandId);

        if (brand is null)
        {
            _logger.LogWarning("Brand {BrandId} not found — aborting provisioning", brandId);
            return;
        }

        var steps = await _db.BrandProvisioningSteps
            .IgnoreQueryFilters()
            .Where(s => s.BrandId == brandId && s.Status == "pending")
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        foreach (var step in steps)
        {
            try
            {
                step.Status = "running";
                step.AttemptCount++;
                await _db.SaveChangesAsync();
                await BroadcastStep(brandId, step.StepName, "running", null);

                var (status, error) = await VerifyStep(step.StepName, brand);

                step.Status = status;
                step.ErrorMessage = error;
                if (status is "completed" or "skipped")
                    step.CompletedAt = DateTimeOffset.UtcNow;

                await _db.SaveChangesAsync();
                await BroadcastStep(brandId, step.StepName, status, error);

                _logger.LogInformation("Step {Step} → {Status} for brand {BrandId}", step.StepName, status, brandId);
            }
            catch (Exception ex)
            {
                step.Status = "failed";
                step.ErrorMessage = ex.Message;
                await _db.SaveChangesAsync();
                await BroadcastStep(brandId, step.StepName, "failed", ex.Message);
                _logger.LogError(ex, "Step {Step} failed for brand {BrandId}", step.StepName, brandId);
            }
        }

        // Mark brand active when all steps are done (completed or skipped)
        var allDone = await _db.BrandProvisioningSteps
            .IgnoreQueryFilters()
            .Where(s => s.BrandId == brandId)
            .AllAsync(s => s.Status == "completed" || s.Status == "skipped");

        if (allDone)
        {
            brand.Status = "active";
            brand.OnboardingCompletedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
            await _hub.Clients.Group($"brand-{brandId}").SendAsync("BrandActivated", brandId);
            _logger.LogInformation("Brand {BrandId} is now active", brandId);
        }
    }

    private async Task<(string status, string? error)> VerifyStep(string stepName, Brand brand)
    {
        return stepName switch
        {
            "wordpress" => await VerifyWordPress(brand),
            "ghl"       => await VerifyGhl(brand),
            _           => ("skipped", null),  // dataforseo, backlinks, email — configured later
        };
    }

    /// <summary>
    /// Verifies WordPress by calling the WP REST API as the configured user.
    /// Uses Application Password auth (username:app-password, base64-encoded).
    /// </summary>
    private async Task<(string, string?)> VerifyWordPress(Brand brand)
    {
        if (string.IsNullOrWhiteSpace(brand.WpSiteUrl)
            || string.IsNullOrWhiteSpace(brand.WpUsername)
            || string.IsNullOrWhiteSpace(brand.WpAppPassword))
        {
            return ("skipped", null);
        }

        var client = _httpFactory.CreateClient("provisioning");
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{brand.WpUsername}:{brand.WpAppPassword}"));

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{brand.WpSiteUrl.TrimEnd('/')}/wp-json/wp/v2/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var resp = await client.SendAsync(request);

        if (resp.IsSuccessStatusCode)
            return ("completed", null);

        var body = await resp.Content.ReadAsStringAsync();
        return ("failed", $"WordPress returned {(int)resp.StatusCode}: {body[..Math.Min(200, body.Length)]}");
    }

    /// <summary>
    /// Verifies GoHighLevel by calling the GHL REST API with the location API key.
    /// </summary>
    private async Task<(string, string?)> VerifyGhl(Brand brand)
    {
        if (string.IsNullOrWhiteSpace(brand.GhlApiKey))
            return ("skipped", null);

        var client = _httpFactory.CreateClient("provisioning");

        var request = new HttpRequestMessage(HttpMethod.Get,
            "https://rest.gohighlevel.com/v1/users/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", brand.GhlApiKey);

        var resp = await client.SendAsync(request);

        if (resp.IsSuccessStatusCode)
            return ("completed", null);

        var body = await resp.Content.ReadAsStringAsync();
        return ("failed", $"GHL returned {(int)resp.StatusCode}: {body[..Math.Min(200, body.Length)]}");
    }

    private Task BroadcastStep(Guid brandId, string stepName, string status, string? error) =>
        _hub.Clients.Group($"brand-{brandId}").SendAsync("StepUpdated", new
        {
            StepName = stepName,
            Status = status,
            ErrorMessage = error,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
}
