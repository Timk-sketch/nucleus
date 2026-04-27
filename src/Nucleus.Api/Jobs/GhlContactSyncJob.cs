using Hangfire;
using Microsoft.EntityFrameworkCore;
using Nucleus.Domain.Entities;
using Nucleus.Infrastructure.Data;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Nucleus.Api.Jobs;

public class GhlContactSyncJob(
    NucleusDbContext db,
    IHttpClientFactory httpFactory,
    ILogger<GhlContactSyncJob> logger)
{
    private const int PageSize = 100;
    private const string GhlBaseUrl = "https://rest.gohighlevel.com/v1";

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task SyncAsync(Guid tenantId, Guid brandId, CancellationToken ct = default)
    {
        var brand = await db.Brands
            .FirstOrDefaultAsync(b => b.Id == brandId && b.TenantId == tenantId, ct);

        if (brand is null || string.IsNullOrEmpty(brand.GhlLocationId) || string.IsNullOrEmpty(brand.GhlApiKey))
        {
            logger.LogInformation("GHL contact sync skipped for brand {BrandId} — no credentials", brandId);
            return;
        }

        var client = httpFactory.CreateClient("ghl");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", brand.GhlApiKey);

        var fetched = 0;
        string? lastId = null;

        while (true)
        {
            var url = $"{GhlBaseUrl}/contacts/?locationId={brand.GhlLocationId}&limit={PageSize}";
            if (lastId is not null) url += $"&startAfterId={lastId}";

            HttpResponseMessage resp;
            try { resp = await client.GetAsync(url, ct); }
            catch (Exception ex)
            {
                logger.LogError(ex, "GHL contacts fetch failed for brand {BrandId}", brandId);
                break;
            }

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("GHL returned {Status} for brand {BrandId}", resp.StatusCode, brandId);
                break;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("contacts", out var contactsEl)) break;

            var contacts = contactsEl.EnumerateArray().ToList();
            if (contacts.Count == 0) break;

            foreach (var c in contacts)
            {
                var ghlId = c.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(ghlId)) continue;

                lastId = ghlId;

                var firstName = c.TryGetProperty("firstName", out var fn) ? fn.GetString() : null;
                var lastName  = c.TryGetProperty("lastName", out var ln) ? ln.GetString() : null;
                var email     = c.TryGetProperty("email", out var em) ? em.GetString() : null;
                var phone     = c.TryGetProperty("phone", out var ph) ? ph.GetString() : null;
                var tags      = c.TryGetProperty("tags", out var tg) ? tg.GetRawText() : null;
                DateTimeOffset? ghlCreated = c.TryGetProperty("dateAdded", out var da)
                    && DateTimeOffset.TryParse(da.GetString(), out var parsedDt) ? parsedDt : null;

                var existing = await db.GhlContacts
                    .FirstOrDefaultAsync(x => x.BrandId == brandId && x.GhlContactId == ghlId, ct);

                if (existing is null)
                {
                    db.GhlContacts.Add(new GhlContact
                    {
                        TenantId = tenantId,
                        BrandId = brandId,
                        GhlContactId = ghlId,
                        FirstName = firstName,
                        LastName = lastName,
                        Email = email,
                        Phone = phone,
                        Tags = tags,
                        GhlCreatedAt = ghlCreated,
                    });
                }
                else
                {
                    existing.FirstName = firstName;
                    existing.LastName = lastName;
                    existing.Email = email;
                    existing.Phone = phone;
                    existing.Tags = tags;
                    existing.GhlCreatedAt = ghlCreated;
                    existing.SyncedAt = DateTimeOffset.UtcNow;
                }
            }

            await db.SaveChangesAsync(ct);
            fetched += contacts.Count;
            if (contacts.Count < PageSize) break;
        }

        logger.LogInformation("GHL sync complete for brand {BrandId}: {Count} contacts upserted", brandId, fetched);
    }
}
