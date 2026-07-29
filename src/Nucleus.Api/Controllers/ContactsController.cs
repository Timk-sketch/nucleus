using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Api.Jobs;
using Nucleus.Application.Common;
using Nucleus.Infrastructure.Data;
using System.Security.Claims;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("api/v1/brands/{brandId:guid}/contacts")]
[Authorize]
[Produces("application/json")]
public class ContactsController(NucleusDbContext db) : ControllerBase
{
    private Guid CurrentTenantId =>
        Guid.Parse(User.FindFirstValue("tenant_id") ?? Guid.Empty.ToString());

    // GET /api/v1/brands/{brandId}/contacts
    [HttpGet]
    public async Task<IActionResult> List(
        Guid brandId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        var brand = await db.Brands
            .Where(b => b.Id == brandId && b.TenantId == CurrentTenantId)
            .Select(b => new { b.Id, b.GhlLocationId })
            .FirstOrDefaultAsync(ct);

        if (brand is null) return NotFound(ApiResponse.Fail("Brand not found."));

        var query = db.GhlContacts.Where(c => c.BrandId == brandId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            query = query.Where(c =>
                (c.FirstName != null && c.FirstName.ToLower().Contains(s)) ||
                (c.LastName  != null && c.LastName.ToLower().Contains(s))  ||
                (c.Email     != null && c.Email.ToLower().Contains(s)));
        }

        var total = await query.CountAsync(ct);
        var contacts = await query
            .OrderByDescending(c => c.GhlCreatedAt)
            .Skip((page - 1) * 50)
            .Take(50)
            .Select(c => new
            {
                c.Id,
                c.GhlContactId,
                c.FirstName,
                c.LastName,
                c.Email,
                c.Phone,
                c.Tags,
                c.GhlCreatedAt,
                c.SyncedAt,
            })
            .ToListAsync(ct);

        return Ok(ApiResponse.Ok(new
        {
            contacts,
            total,
            page,
            ghlConfigured = !string.IsNullOrEmpty(brand.GhlLocationId),
        }));
    }

    // GET /api/v1/brands/{brandId}/contacts/{contactId}
    [HttpGet("{contactId:guid}")]
    public async Task<IActionResult> GetContact(
        Guid brandId,
        Guid contactId,
        CancellationToken ct)
    {
        var contact = await db.GhlContacts
            .Where(c => c.Id == contactId && c.BrandId == brandId && c.TenantId == CurrentTenantId)
            .Select(c => new
            {
                c.Id,
                c.FirstName,
                c.LastName,
                c.Email,
                c.Phone,
                c.Tags,
                c.GhlCreatedAt,
                c.SyncedAt,
            })
            .FirstOrDefaultAsync(ct);

        if (contact is null) return NotFound(ApiResponse.Fail("Contact not found."));

        // Parse Tags JSON array into List<string>
        List<string> parsedTags = [];
        if (!string.IsNullOrEmpty(contact.Tags))
        {
            try { parsedTags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(contact.Tags) ?? []; }
            catch { /* ignore malformed tags */ }
        }

        return Ok(ApiResponse.Ok(new
        {
            contact.Id,
            contact.FirstName,
            contact.LastName,
            contact.Email,
            contact.Phone,
            Tags = parsedTags,
            contact.GhlCreatedAt,
            contact.SyncedAt,
        }));
    }

    // GET /api/v1/brands/{brandId}/contacts/{contactId}/leads
    [HttpGet("{contactId:guid}/leads")]
    public async Task<IActionResult> GetContactLeads(
        Guid brandId,
        Guid contactId,
        CancellationToken ct)
    {
        // Verify contact belongs to brand + tenant
        var contact = await db.GhlContacts
            .Where(c => c.Id == contactId && c.BrandId == brandId && c.TenantId == CurrentTenantId)
            .Select(c => new { c.Email })
            .FirstOrDefaultAsync(ct);

        if (contact is null) return NotFound(ApiResponse.Fail("Contact not found."));

        if (string.IsNullOrEmpty(contact.Email))
            return Ok(ApiResponse.Ok(Array.Empty<object>()));

        var email = contact.Email.ToLower();

        // Finder sessions for this brand matched by lead email
        var leads = await (
            from fs in db.FinderSessions
            join f in db.Finders on fs.FinderId equals f.Id
            where f.BrandId == brandId
               && fs.LeadEmail != null
               && fs.LeadEmail.ToLower() == email
            orderby fs.CreatedAt descending
            select new
            {
                fs.Id,
                FinderName = f.Name,
                fs.Converted,
                fs.CompletedAt,
                fs.CreatedAt,
            }
        ).Take(20).ToListAsync(ct);

        return Ok(ApiResponse.Ok(leads));
    }

    // POST /api/v1/brands/{brandId}/contacts/sync
    [HttpPost("sync")]
    public async Task<IActionResult> TriggerSync(Guid brandId, CancellationToken ct)
    {
        var brand = await db.Brands
            .Where(b => b.Id == brandId && b.TenantId == CurrentTenantId)
            .Select(b => new { b.Id })
            .FirstOrDefaultAsync(ct);

        if (brand is null) return NotFound(ApiResponse.Fail("Brand not found."));

        var tenantId = CurrentTenantId;
        BackgroundJob.Enqueue<GhlContactSyncJob>(j =>
            j.SyncAsync(tenantId, brandId, CancellationToken.None));

        return Ok(ApiResponse.Ok(new { queued = true }));
    }
}
