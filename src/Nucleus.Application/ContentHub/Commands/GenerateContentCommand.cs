using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ContentHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.ContentHub.Commands;

/// <summary>
/// Generates AI content for a brand using Claude.
/// Enforces plan gates: starter = max 5 generations/month, pro/agency = unlimited.
/// Records AI cost in AiUsage after every successful generation.
/// Returns the created ContentPage with generated HTML.
/// </summary>
public record GenerateContentCommand(
    Guid BrandId,
    string Title,
    string PageType,
    string? FocusKeyword,
    Guid? KeywordId,
    int WordCount,
    string? CustomPrompt,
    Guid? TemplateId) : IRequest<GenerateContentResult>;

public record GenerateContentResult(
    bool Success,
    ContentPageDto? ContentPage,
    string? ErrorMessage,
    bool PlanLimitReached = false);

public class GenerateContentValidator : AbstractValidator<GenerateContentCommand>
{
    public GenerateContentValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.PageType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.WordCount).InclusiveBetween(100, 5000);
    }
}

public class GenerateContentHandler : IRequestHandler<GenerateContentCommand, GenerateContentResult>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;
    private readonly IClaudeService _claude;
    private readonly ITenantPlanService _plan;

    public GenerateContentHandler(
        INucleusDbContext db,
        ICurrentTenantService tenant,
        IClaudeService claude,
        ITenantPlanService plan)
    {
        _db = db;
        _tenant = tenant;
        _claude = claude;
        _plan = plan;
    }

    public async Task<GenerateContentResult> Handle(
        GenerateContentCommand request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brand = await _db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId)
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null)
            return new GenerateContentResult(false, null, "Brand not found.");

        // Plan gate via TenantPlanService
        if (!await _plan.IsFeatureAllowedAsync("content_generation", cancellationToken))
        {
            var usageCount = await _plan.GetMonthlyUsageAsync("content_generation", cancellationToken);
            var currentPlan = await _plan.GetPlanAsync(cancellationToken);
            return new GenerateContentResult(false, null,
                currentPlan == "starter"
                    ? $"Starter plan is limited to 5 AI generations per month ({usageCount} used). Upgrade to Pro for unlimited."
                    : "AI content generation is not available on your current plan.",
                PlanLimitReached: true);
        }

        // Resolve keyword text if provided
        string? keywordText = null;
        if (request.KeywordId.HasValue)
        {
            keywordText = await _db.BrandKeywords
                .Where(k => k.Id == request.KeywordId.Value && k.BrandId == request.BrandId)
                .Select(k => k.Keyword)
                .FirstOrDefaultAsync(cancellationToken);
        }
        keywordText ??= request.FocusKeyword;

        // Load template body if requested
        string? templateBody = null;
        if (request.TemplateId.HasValue)
        {
            templateBody = await _db.ContentTemplates
                .Where(t => t.Id == request.TemplateId.Value
                         && (t.BrandId == request.BrandId || t.IsGlobal)
                         && t.IsActive)
                .Select(t => t.Body)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Load banned words for this brand
        var bannedWords = await _db.BannedWords
            .Where(w => w.BrandId == request.BrandId)
            .Select(w => w.Word)
            .ToListAsync(cancellationToken);

        // Generate content via Claude API
        var systemPrompt = BuildSystemPrompt(brand.Name, bannedWords, templateBody);
        var userPrompt = BuildUserPrompt(request, keywordText, brand.Name);
        var generatedHtml = await _claude.GenerateAsync(
            systemPrompt, userPrompt,
            model: "claude-sonnet-4-6",
            maxTokens: Math.Min(8000, request.WordCount * 8),
            ct: cancellationToken);

        // Count words in generated HTML
        var wordCount = CountWords(generatedHtml);

        // Persist the content page
        var page = new ContentPage
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            KeywordId = request.KeywordId,
            Title = request.Title,
            PageType = request.PageType,
            Status = "draft",
            HtmlContent = generatedHtml,
            SeoTitle = request.Title,
            MetaDescription = $"Learn about {request.Title} — an in-depth guide for {brand.Name}.",
            AiModel = "claude-sonnet-4-6",
            AiPrompt = BuildUserPrompt(request, keywordText, brand.Name),
            WordCount = wordCount,
        };
        _db.ContentPages.Add(page);

        // Record AI usage for cost tracking and plan enforcement
        var usage = new AiUsage
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            Feature = "content_generation",
            TokensUsed = EstimateTokens(wordCount),
            CostUsd = EstimateCost(wordCount),
            Model = "claude-sonnet-4-6",
            ContentPageId = page.Id,
        };
        _db.AiUsages.Add(usage);

        await _db.SaveChangesAsync(cancellationToken);

        return new GenerateContentResult(true, new ContentPageDto
        {
            Id = page.Id,
            BrandId = page.BrandId,
            KeywordId = page.KeywordId,
            KeywordText = keywordText,
            Title = page.Title,
            PageType = page.PageType,
            Status = page.Status,
            HtmlContent = page.HtmlContent,
            SeoTitle = page.SeoTitle,
            MetaDescription = page.MetaDescription,
            AiModel = page.AiModel,
            WordCount = page.WordCount,
            CreatedAt = page.CreatedAt,
            UpdatedAt = page.UpdatedAt,
        }, null);
    }

    // ── Prompt builders ───────────────────────────────────────────────────────

    private static string BuildSystemPrompt(string brandName, List<string> bannedWords, string? templateBody)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"You are a professional SEO content writer for {brandName}.");
        sb.AppendLine("Write well-structured HTML content using semantic tags (h1, h2, h3, p, ul, ol, strong).");
        sb.AppendLine("Return ONLY the HTML — no markdown fences, no preamble, no explanation.");
        sb.AppendLine("Include an <h1> for the title, multiple <h2> sections, and a clear conclusion.");

        if (bannedWords.Count > 0)
            sb.AppendLine($"NEVER use these words: {string.Join(", ", bannedWords)}.");

        if (!string.IsNullOrWhiteSpace(templateBody))
            sb.AppendLine($"Follow this structure/template:\n{templateBody[..Math.Min(800, templateBody.Length)]}");

        return sb.ToString();
    }

    private static string BuildUserPrompt(GenerateContentCommand req, string? keywordText, string brandName)
    {
        if (!string.IsNullOrWhiteSpace(req.CustomPrompt))
            return req.CustomPrompt;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Write a {req.WordCount}-word {req.PageType.Replace("_", " ")} titled: \"{req.Title}\".");

        if (!string.IsNullOrWhiteSpace(keywordText))
            sb.AppendLine($"Primary keyword to optimise for: {keywordText}");

        sb.AppendLine($"Brand: {brandName}");
        sb.AppendLine("Use clear headings, bullet points where appropriate, and a strong CTA at the end.");

        return sb.ToString();
    }

    private static int CountWords(string html)
    {
        // Strip basic HTML tags for word counting
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        return text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static int EstimateTokens(int wordCount)
        => (int)(wordCount * 1.3); // ~1.3 tokens per word average

    private static decimal EstimateCost(int wordCount)
    {
        // Claude 3.5 Sonnet pricing (approximate): $3/M input tokens + $15/M output tokens
        var outputTokens = EstimateTokens(wordCount);
        var inputTokens = 500; // system + prompt tokens
        return (inputTokens * 0.000003m) + (outputTokens * 0.000015m);
    }
}
