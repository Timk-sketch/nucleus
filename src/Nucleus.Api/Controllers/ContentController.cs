using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common;
using Nucleus.Api.Jobs;
using Nucleus.Domain.Entities;
using Nucleus.Infrastructure.Data;
using System.Security.Claims;

namespace Nucleus.Api.Controllers;

[ApiController]
[Route("api/v1/brands/{brandId:guid}")]
[Authorize]
[Produces("application/json")]
public class ContentController(
    NucleusDbContext db,
    IHttpClientFactory httpFactory,
    ILogger<ContentController> logger) : ControllerBase
{
    private Guid CurrentTenantId =>
        Guid.Parse(User.FindFirstValue("tenant_id") ?? Guid.Empty.ToString());

    // ── WordPress Posts ──────────────────────────────────────────────────────

    // GET /api/v1/brands/{brandId}/posts
    [HttpGet("posts")]
    public async Task<IActionResult> ListPosts(Guid brandId, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        var brand = await GetBrand(brandId, ct);
        if (brand is null) return NotFound(ApiResponse.Fail("Brand not found."));

        if (string.IsNullOrWhiteSpace(brand.WpSiteUrl))
            return Ok(ApiResponse.Ok(new { posts = Array.Empty<object>(), wpConfigured = false }));

        try
        {
            var client = CreateWpClient(brand);
            var url = $"{brand.WpSiteUrl.TrimEnd('/')}/wp-json/wp/v2/posts?per_page=20&page={page}&orderby=date&order=desc&_fields=id,title,status,date,link,excerpt";
            var resp = await client.GetAsync(url, ct);

            if (!resp.IsSuccessStatusCode)
                return StatusCode((int)resp.StatusCode, ApiResponse.Fail($"WordPress returned {(int)resp.StatusCode}"));

            var json = await resp.Content.ReadAsStringAsync(ct);
            var posts = JsonDocument.Parse(json).RootElement;
            var total = resp.Headers.TryGetValues("X-WP-Total", out var t) ? t.FirstOrDefault() : null;
            var totalPages = resp.Headers.TryGetValues("X-WP-TotalPages", out var tp) ? tp.FirstOrDefault() : null;

            return Ok(ApiResponse.Ok(new
            {
                wpConfigured = true,
                posts,
                total = total != null ? int.Parse(total) : (int?)null,
                totalPages = totalPages != null ? int.Parse(totalPages) : (int?)null,
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch WP posts for brand {BrandId}", brandId);
            return StatusCode(502, ApiResponse.Fail($"Could not reach WordPress: {ex.Message}"));
        }
    }

    // POST /api/v1/brands/{brandId}/posts
    [HttpPost("posts")]
    public async Task<IActionResult> CreatePost(Guid brandId, [FromBody] CreatePostRequest req, CancellationToken ct)
    {
        var brand = await GetBrand(brandId, ct);
        if (brand is null) return NotFound(ApiResponse.Fail("Brand not found."));

        if (string.IsNullOrWhiteSpace(brand.WpSiteUrl))
            return BadRequest(ApiResponse.Fail("WordPress is not configured for this brand."));

        try
        {
            var client = CreateWpClient(brand);
            var payload = new
            {
                title = req.Title,
                content = req.Content,
                excerpt = req.Excerpt ?? "",
                status = req.Publish ? "publish" : "draft",
            };

            var resp = await client.PostAsJsonAsync(
                $"{brand.WpSiteUrl.TrimEnd('/')}/wp-json/wp/v2/posts", payload, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                return StatusCode((int)resp.StatusCode, ApiResponse.Fail($"WordPress error: {err[..Math.Min(300, err.Length)]}"));
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            var post = JsonDocument.Parse(json).RootElement;
            return Ok(ApiResponse.Ok(post));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create WP post for brand {BrandId}", brandId);
            return StatusCode(502, ApiResponse.Fail($"Could not reach WordPress: {ex.Message}"));
        }
    }

    // ── Keywords ─────────────────────────────────────────────────────────────

    // GET /api/v1/brands/{brandId}/keywords
    [HttpGet("keywords")]
    public async Task<IActionResult> ListKeywords(Guid brandId, CancellationToken ct)
    {
        var brand = await GetBrand(brandId, ct);
        if (brand is null) return NotFound(ApiResponse.Fail("Brand not found."));

        var keywords = await db.BrandKeywords
            .Where(k => k.BrandId == brandId)
            .OrderBy(k => k.Keyword)
            .Select(k => new { k.Id, k.Keyword, k.TargetUrl, k.Notes, k.CreatedAt })
            .ToListAsync(ct);

        return Ok(ApiResponse.Ok(keywords));
    }

    // POST /api/v1/brands/{brandId}/keywords
    [HttpPost("keywords")]
    public async Task<IActionResult> AddKeyword(Guid brandId, [FromBody] KeywordRequest req, CancellationToken ct)
    {
        var brand = await GetBrand(brandId, ct);
        if (brand is null) return NotFound(ApiResponse.Fail("Brand not found."));

        if (string.IsNullOrWhiteSpace(req.Keyword))
            return BadRequest(ApiResponse.Fail("Keyword is required."));

        var keyword = new BrandKeyword
        {
            TenantId = CurrentTenantId,
            BrandId = brandId,
            Keyword = req.Keyword.Trim().ToLowerInvariant(),
            TargetUrl = req.TargetUrl?.Trim(),
            Notes = req.Notes?.Trim(),
        };

        db.BrandKeywords.Add(keyword);
        await db.SaveChangesAsync(ct);

        return Ok(ApiResponse.Ok(new { keyword.Id, keyword.Keyword, keyword.TargetUrl, keyword.Notes }));
    }

    // GET /api/v1/brands/{brandId}/keywords/ranks
    // Returns latest rank snapshot for every keyword in the brand
    [HttpGet("keywords/ranks")]
    public async Task<IActionResult> ListRanks(Guid brandId, CancellationToken ct)
    {
        var brand = await GetBrand(brandId, ct);
        if (brand is null) return NotFound(ApiResponse.Fail("Brand not found."));

        // Latest rank per keyword (one row per keyword)
        var ranks = await db.KeywordRanks
            .Where(r => r.BrandId == brandId)
            .GroupBy(r => r.KeywordId)
            .Select(g => g.OrderByDescending(r => r.CheckedAt).First())
            .Select(r => new
            {
                r.KeywordId,
                r.Position,
                r.PreviousPosition,
                r.SearchVolume,
                r.KeywordDifficulty,
                r.RankedUrl,
                r.CheckedAt,
            })
            .ToListAsync(ct);

        return Ok(ApiResponse.Ok(ranks));
    }

    // POST /api/v1/brands/{brandId}/keywords/ranks/check
    // Enqueues a DataForSEO rank check for all keywords in the brand
    [HttpPost("keywords/ranks/check")]
    public async Task<IActionResult> CheckRanks(Guid brandId, CancellationToken ct)
    {
        var brand = await GetBrand(brandId, ct);
        if (brand is null) return NotFound(ApiResponse.Fail("Brand not found."));

        BackgroundJob.Enqueue<KeywordRankJob>(j =>
            j.CheckRanksAsync(CurrentTenantId, brandId, CancellationToken.None));

        return Ok(ApiResponse.Ok(new { queued = true }));
    }

    // DELETE /api/v1/brands/{brandId}/keywords/{keywordId}
    [HttpDelete("keywords/{keywordId:guid}")]
    public async Task<IActionResult> RemoveKeyword(Guid brandId, Guid keywordId, CancellationToken ct)
    {
        var kw = await db.BrandKeywords
            .FirstOrDefaultAsync(k => k.Id == keywordId && k.BrandId == brandId, ct);

        if (kw is null) return NotFound(ApiResponse.Fail("Keyword not found."));

        db.BrandKeywords.Remove(kw);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Brand?> GetBrand(Guid brandId, CancellationToken ct) =>
        await db.Brands.FirstOrDefaultAsync(b => b.Id == brandId && b.TenantId == CurrentTenantId, ct);

    private HttpClient CreateWpClient(Brand brand)
    {
        var client = httpFactory.CreateClient("wordpress");
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{brand.WpUsername}:{brand.WpAppPassword}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return client;
    }
}

public record CreatePostRequest(string Title, string Content, string? Excerpt, bool Publish);
public record KeywordRequest(string Keyword, string? TargetUrl, string? Notes);
