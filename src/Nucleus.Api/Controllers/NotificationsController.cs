using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Infrastructure.Data;

namespace Nucleus.Api.Controllers;

/// <summary>
/// Notification feed — unified view of search alerts + brand mentions.
///
/// GET  /api/notifications?brandId=      — merged feed, newest first
/// GET  /api/notifications/count?brandId= — unread count for bell badge
/// DEL  /api/notifications/alerts/{id}   — dismiss alert
/// PUT  /api/notifications/mentions/{id}/reviewed — mark mention reviewed
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
[Produces("application/json")]
public class NotificationsController(
    NucleusDbContext db,
    ICurrentTenantService tenant) : ControllerBase
{
    // GET /api/notifications?brandId=
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid brandId, CancellationToken ct)
    {
        if (brandId == Guid.Empty)
            return BadRequest(ApiResponse.Fail("brandId is required."));

        var tenantId = tenant.TenantId;

        var alerts = await db.SearchAlerts
            .Where(a => a.BrandId == brandId && a.TenantId == tenantId
                     && a.IsActive && a.TriggeredAt != null)
            .Select(a => new NotifItem
            {
                Id        = a.Id,
                Type      = "alert",
                Title     = a.Keyword.Keyword,
                Body      = a.Message ?? $"{a.AlertType.Replace("_", " ")} alert",
                Date      = a.TriggeredAt!.Value,
                AlertType = a.AlertType,
            })
            .ToListAsync(ct);

        var rawMentions = await db.BrandMentions
            .Where(m => m.BrandId == brandId && m.TenantId == tenantId && !m.IsReviewed)
            .Select(m => new { m.Id, m.MentionText, m.DiscoveredAt, m.Sentiment, m.SourceUrl })
            .ToListAsync(ct);

        var mentions = rawMentions.Select(m => new NotifItem
        {
            Id        = m.Id,
            Type      = "mention",
            Title     = "Brand mention",
            Body      = m.MentionText.Length > 140 ? m.MentionText[..140] + "…" : m.MentionText,
            Date      = m.DiscoveredAt,
            Sentiment = m.Sentiment,
            Url       = m.SourceUrl,
        }).ToList();

        var feed = alerts.Concat(mentions)
            .OrderByDescending(n => n.Date)
            .ToList();

        return Ok(ApiResponse.Ok(feed));
    }

    // GET /api/notifications/count?brandId=
    [HttpGet("count")]
    public async Task<IActionResult> Count([FromQuery] Guid brandId, CancellationToken ct)
    {
        if (brandId == Guid.Empty) return Ok(ApiResponse.Ok(new { count = 0 }));

        var tenantId = tenant.TenantId;

        var alertCount = await db.SearchAlerts
            .CountAsync(a => a.BrandId == brandId && a.TenantId == tenantId
                          && a.IsActive && a.TriggeredAt != null, ct);

        var mentionCount = await db.BrandMentions
            .CountAsync(m => m.BrandId == brandId && m.TenantId == tenantId && !m.IsReviewed, ct);

        return Ok(ApiResponse.Ok(new { count = alertCount + mentionCount }));
    }

    // DELETE /api/notifications/alerts/{alertId}
    [HttpDelete("alerts/{alertId:guid}")]
    public async Task<IActionResult> DismissAlert(Guid alertId, CancellationToken ct)
    {
        var alert = await db.SearchAlerts
            .FirstOrDefaultAsync(a => a.Id == alertId && a.TenantId == tenant.TenantId, ct);

        if (alert is null) return NotFound(ApiResponse.Fail("Alert not found."));

        alert.IsActive = false;
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok(new { dismissed = true }));
    }

    // PUT /api/notifications/mentions/{mentionId}/reviewed
    [HttpPut("mentions/{mentionId:guid}/reviewed")]
    public async Task<IActionResult> ReviewMention(Guid mentionId, CancellationToken ct)
    {
        var mention = await db.BrandMentions
            .FirstOrDefaultAsync(m => m.Id == mentionId && m.TenantId == tenant.TenantId, ct);

        if (mention is null) return NotFound(ApiResponse.Fail("Mention not found."));

        mention.IsReviewed = true;
        await db.SaveChangesAsync(ct);
        return Ok(ApiResponse.Ok(new { reviewed = true }));
    }
}

file sealed class NotifItem
{
    public Guid              Id        { get; set; }
    public string            Type      { get; set; } = "";
    public string            Title     { get; set; } = "";
    public string            Body      { get; set; } = "";
    public DateTimeOffset    Date      { get; set; }
    public string?           AlertType { get; set; }
    public string?           Sentiment { get; set; }
    public string?           Url       { get; set; }
}
