using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;
using Nucleus.Infrastructure.Data;
using System.Security.Claims;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("api/v1/brands/{brandId:guid}/campaigns")]
[Authorize]
[Produces("application/json")]
public class EmailCampaignController(
    NucleusDbContext db,
    IEmailService emailService,
    ILogger<EmailCampaignController> logger) : ControllerBase
{
    private Guid CurrentTenantId =>
        Guid.Parse(User.FindFirstValue("tenant_id") ?? Guid.Empty.ToString());

    // GET /api/v1/brands/{brandId}/campaigns
    [HttpGet]
    public async Task<IActionResult> List(Guid brandId, CancellationToken ct)
    {
        if (!await BrandExists(brandId, ct)) return NotFound(ApiResponse.Fail("Brand not found."));

        var campaigns = await db.EmailCampaigns
            .Where(c => c.BrandId == brandId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id, c.Subject, c.Status, c.RecipientCount, c.SentAt, c.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(ApiResponse.Ok(campaigns));
    }

    // POST /api/v1/brands/{brandId}/campaigns
    [HttpPost]
    public async Task<IActionResult> Create(Guid brandId, [FromBody] CreateCampaignRequest req, CancellationToken ct)
    {
        if (!await BrandExists(brandId, ct)) return NotFound(ApiResponse.Fail("Brand not found."));

        if (string.IsNullOrWhiteSpace(req.Subject))
            return BadRequest(ApiResponse.Fail("Subject is required."));

        var campaign = new EmailCampaign
        {
            TenantId = CurrentTenantId,
            BrandId = brandId,
            Subject = req.Subject.Trim(),
            HtmlBody = req.HtmlBody?.Trim() ?? string.Empty,
            Status = "draft",
        };

        db.EmailCampaigns.Add(campaign);
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse.Ok(new { campaign.Id, campaign.Subject, campaign.Status }));
    }

    // POST /api/v1/brands/{brandId}/campaigns/{id}/send
    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid brandId, Guid id, [FromBody] SendCampaignRequest req, CancellationToken ct)
    {
        if (!await BrandExists(brandId, ct)) return NotFound(ApiResponse.Fail("Brand not found."));

        var campaign = await db.EmailCampaigns
            .FirstOrDefaultAsync(c => c.Id == id && c.BrandId == brandId, ct);

        if (campaign is null) return NotFound(ApiResponse.Fail("Campaign not found."));
        if (campaign.Status == "sent") return BadRequest(ApiResponse.Fail("Campaign already sent."));

        if (!emailService.IsConfigured)
            return BadRequest(ApiResponse.Fail("Email (SMTP) is not configured. Set SMTP_HOST, SMTP_USER, and SMTP_PASS."));

        var recipients = (req.ToEmails ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(e => e.Contains('@'))
            .Distinct()
            .ToList();

        if (recipients.Count == 0)
            return BadRequest(ApiResponse.Fail("At least one valid recipient email is required."));

        campaign.Status = "sending";
        campaign.ToEmails = string.Join(",", recipients);
        await db.SaveChangesAsync(ct);

        var errors = 0;
        foreach (var to in recipients)
        {
            try { await emailService.SendAsync(to, campaign.Subject, campaign.HtmlBody, ct); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send campaign {Id} to {Email}", id, to);
                errors++;
            }
        }

        campaign.Status = errors == recipients.Count ? "failed" : "sent";
        campaign.RecipientCount = recipients.Count - errors;
        campaign.SentAt = DateTimeOffset.UtcNow;
        campaign.ErrorMessage = errors > 0 ? $"{errors}/{recipients.Count} deliveries failed." : null;
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse.Ok(new { campaign.Status, campaign.RecipientCount, campaign.SentAt }));
    }

    // DELETE /api/v1/brands/{brandId}/campaigns/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid brandId, Guid id, CancellationToken ct)
    {
        var campaign = await db.EmailCampaigns
            .FirstOrDefaultAsync(c => c.Id == id && c.BrandId == brandId, ct);

        if (campaign is null) return NotFound(ApiResponse.Fail("Campaign not found."));
        if (campaign.Status == "sent") return BadRequest(ApiResponse.Fail("Cannot delete a sent campaign."));

        db.EmailCampaigns.Remove(campaign);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<bool> BrandExists(Guid brandId, CancellationToken ct) =>
        await db.Brands.AnyAsync(b => b.Id == brandId && b.TenantId == CurrentTenantId, ct);
}

public record CreateCampaignRequest(string Subject, string? HtmlBody);
public record SendCampaignRequest(string ToEmails);
