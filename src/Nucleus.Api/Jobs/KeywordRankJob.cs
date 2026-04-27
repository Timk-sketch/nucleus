using Hangfire;
using Microsoft.EntityFrameworkCore;
using Nucleus.Domain.Entities;
using Nucleus.Infrastructure.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Nucleus.Api.Jobs;

public class KeywordRankJob(
    NucleusDbContext db,
    IHttpClientFactory httpFactory,
    IConfiguration config,
    ILogger<KeywordRankJob> logger)
{
    private const string DataForSeoUrl = "https://api.dataforseo.com/v3/serp/google/organic/live/regular";

    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task CheckRanksAsync(Guid tenantId, Guid brandId, CancellationToken ct = default)
    {
        var login    = config["DATAFORSEO_LOGIN"];
        var password = config["DATAFORSEO_PASSWORD"];

        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            logger.LogInformation("DataForSEO credentials not configured — keyword rank check skipped");
            return;
        }

        var brand = await db.Brands.FirstOrDefaultAsync(b => b.Id == brandId && b.TenantId == tenantId, ct);
        if (brand is null) return;

        var keywords = await db.BrandKeywords
            .Where(k => k.BrandId == brandId)
            .Select(k => new { k.Id, k.Keyword, k.TargetUrl })
            .ToListAsync(ct);

        if (keywords.Count == 0) return;

        var client = httpFactory.CreateClient("dataforseo");
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{login}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);

        // DataForSEO processes up to 100 tasks per request — batch them
        foreach (var batch in keywords.Chunk(50))
        {
            var tasks = batch.Select(k => new
            {
                keyword = k.Keyword,
                location_code = 2840, // United States
                language_code = "en",
                device = "desktop",
                os = "windows",
            }).ToArray();

            var payload = JsonSerializer.Serialize(tasks);
            HttpResponseMessage resp;
            try
            {
                resp = await client.PostAsync(DataForSeoUrl,
                    new StringContent(payload, Encoding.UTF8, "application/json"), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DataForSEO request failed for brand {BrandId}", brandId);
                continue;
            }

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("DataForSEO returned {Status} for brand {BrandId}", resp.StatusCode, brandId);
                continue;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("tasks", out var tasksEl)) continue;

            var taskList = tasksEl.EnumerateArray().ToList();
            for (int i = 0; i < taskList.Count && i < batch.Length; i++)
            {
                var kwInfo = batch[i];
                var task = taskList[i];

                if (!task.TryGetProperty("result", out var resultEl)) continue;
                var results = resultEl.EnumerateArray().ToList();
                if (results.Count == 0) continue;

                var firstResult = results[0];
                int? position = null;
                string? rankedUrl = null;

                if (firstResult.TryGetProperty("items", out var itemsEl))
                {
                    foreach (var item in itemsEl.EnumerateArray())
                    {
                        if (!item.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "organic") continue;
                        if (item.TryGetProperty("rank_absolute", out var rankEl))
                            position = rankEl.GetInt32();
                        if (item.TryGetProperty("url", out var urlEl))
                            rankedUrl = urlEl.GetString();
                        break;
                    }
                }

                // Get previous latest rank
                var previous = await db.KeywordRanks
                    .Where(r => r.KeywordId == kwInfo.Id)
                    .OrderByDescending(r => r.CheckedAt)
                    .Select(r => r.Position)
                    .FirstOrDefaultAsync(ct);

                db.KeywordRanks.Add(new KeywordRank
                {
                    TenantId = tenantId,
                    BrandId = brandId,
                    KeywordId = kwInfo.Id,
                    Position = position,
                    PreviousPosition = previous,
                    RankedUrl = rankedUrl,
                    CheckedAt = DateTimeOffset.UtcNow,
                });
            }

            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Keyword rank check complete for brand {BrandId}: {Count} keywords", brandId, keywords.Count);
    }
}
