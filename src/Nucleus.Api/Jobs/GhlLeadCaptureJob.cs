using Microsoft.EntityFrameworkCore;
using Nucleus.Infrastructure.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Nucleus.Api.Jobs;

/// <summary>
/// Hangfire job: creates a GHL contact from a converted FinderSession.
/// Silently skips if the brand has no GHL credentials configured.
/// Enqueued by RecordFinderConversionCommand after a session is marked converted.
/// </summary>
public class GhlLeadCaptureJob(
    NucleusDbContext db,
    IHttpClientFactory httpFactory,
    ILogger<GhlLeadCaptureJob> logger)
{
    private const string GhlBaseUrl = "https://rest.gohighlevel.com/v1";

    public async Task CaptureAsync(Guid sessionId, CancellationToken ct = default)
    {
        // Load session + finder + brand (no tenant filter — job runs outside HTTP context)
        var session = await db.FinderSessions
            .IgnoreQueryFilters()
            .Include(s => s.Finder)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null)
        {
            logger.LogWarning("GHL lead capture: session {SessionId} not found", sessionId);
            return;
        }

        // Nothing to send if no contact info was captured
        if (string.IsNullOrEmpty(session.LeadEmail) && string.IsNullOrEmpty(session.LeadPhone))
        {
            logger.LogDebug("GHL lead capture: session {SessionId} has no lead info — skipping", sessionId);
            return;
        }

        var brand = await db.Brands
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == session.Finder.BrandId, ct);

        if (brand is null || string.IsNullOrEmpty(brand.GhlApiKey) || string.IsNullOrEmpty(brand.GhlLocationId))
        {
            logger.LogInformation("GHL lead capture: brand {BrandId} has no GHL credentials — skipping", session.Finder.BrandId);
            return;
        }

        // Split name if provided
        var nameParts = (session.LeadName ?? string.Empty).Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts.Length > 0 ? nameParts[0] : null;
        var lastName  = nameParts.Length > 1 ? nameParts[1] : null;

        var payload = new
        {
            locationId = brand.GhlLocationId,
            firstName,
            lastName,
            email  = session.LeadEmail,
            phone  = session.LeadPhone,
            source = $"Finder: {session.Finder.Name}",
            tags   = new[] { "finder-lead", session.Finder.Slug },
        };

        var client = httpFactory.CreateClient("ghl");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", brand.GhlApiKey);

        var body = new StringContent(
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }),
            Encoding.UTF8,
            "application/json");

        try
        {
            var resp = await client.PostAsync($"{GhlBaseUrl}/contacts/", body, ct);
            if (resp.IsSuccessStatusCode)
                logger.LogInformation("GHL contact created for session {SessionId}", sessionId);
            else
            {
                var error = await resp.Content.ReadAsStringAsync(ct);
                logger.LogWarning("GHL contact creation failed for session {SessionId}: {Status} {Error}",
                    sessionId, resp.StatusCode, error);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GHL lead capture failed for session {SessionId}", sessionId);
            throw; // let Hangfire retry
        }
    }
}
