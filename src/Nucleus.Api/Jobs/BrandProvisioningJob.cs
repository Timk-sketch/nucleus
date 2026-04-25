using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Api.Hubs;
using Nucleus.Infrastructure.Data;

namespace Nucleus.Api.Jobs;

/// <summary>
/// Simulates provisioning each brand integration step.
/// Run as a Hangfire background job — no HTTP context, so IgnoreQueryFilters() is required.
/// </summary>
public class BrandProvisioningJob
{
    private readonly NucleusDbContext _db;
    private readonly IHubContext<ProvisioningHub> _hub;
    private readonly ILogger<BrandProvisioningJob> _logger;

    public BrandProvisioningJob(
        NucleusDbContext db,
        IHubContext<ProvisioningHub> hub,
        ILogger<BrandProvisioningJob> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task RunAsync(Guid brandId)
    {
        _logger.LogInformation("Provisioning brand {BrandId}", brandId);

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

                // Simulate integration work (2–4s per step)
                await Task.Delay(TimeSpan.FromSeconds(2));

                step.Status = "completed";
                step.CompletedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync();
                await BroadcastStep(brandId, step.StepName, "completed", null);

                _logger.LogInformation("Step {Step} completed for brand {BrandId}", step.StepName, brandId);
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

        // Mark brand active when all steps complete
        var allDone = await _db.BrandProvisioningSteps
            .IgnoreQueryFilters()
            .Where(s => s.BrandId == brandId)
            .AllAsync(s => s.Status == "completed");

        if (allDone)
        {
            var brand = await _db.Brands.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Id == brandId);
            if (brand is not null)
            {
                brand.Status = "active";
                brand.OnboardingCompletedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync();
                await _hub.Clients.Group($"brand-{brandId}").SendAsync("BrandActivated", brandId);
                _logger.LogInformation("Brand {BrandId} is now active", brandId);
            }
        }
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
