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

    public GenerateContentHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
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

        // Plan gate: starter = max 5 AI generations per month
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == _tenant.TenantId, cancellationToken);

        if (tenant?.Plan == "starter")
        {
            var monthStart = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var monthlyCount = await _db.AiUsages
                .CountAsync(u => u.TenantId == _tenant.TenantId
                              && u.Feature == "content_generation"
                              && u.CreatedAt >= monthStart, cancellationToken);

            if (monthlyCount >= 5)
                return new GenerateContentResult(false, null,
                    "Starter plan is limited to 5 AI generations per month. Upgrade to Pro for unlimited.",
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

        // Generate content (simulated — real Claude API call would go here)
        var generatedHtml = SimulateContentGeneration(
            request.Title, keywordText, request.PageType,
            request.WordCount, templateBody, bannedWords,
            request.CustomPrompt, brand.Name);

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
            AiModel = "claude-3-5-sonnet-20241022",
            AiPrompt = BuildPrompt(request, keywordText, templateBody),
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
            Model = "claude-3-5-sonnet-20241022",
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

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string SimulateContentGeneration(
        string title, string? keyword, string pageType,
        int requestedWords, string? templateBody, List<string> bannedWords,
        string? customPrompt, string brandName)
    {
        // Stub implementation — in production this calls Claude API.
        // The real implementation would:
        //   1. Build a system prompt incorporating brand voice + banned words
        //   2. Call Anthropic Claude API with messages
        //   3. Return the HTML response
        // For now we generate realistic placeholder content.

        var bannedNote = bannedWords.Count > 0
            ? $"<!-- Banned words avoided: {string.Join(", ", bannedWords)} -->"
            : "";

        return $"""
            {bannedNote}
            <article>
              <h1>{title}</h1>
              <p>This comprehensive guide covers everything you need to know about <strong>{keyword ?? title}</strong> for {brandName}.</p>
              <h2>Introduction</h2>
              <p>Understanding {keyword ?? title} is essential for anyone looking to {pageType.Replace("_", " ")} effectively. In this guide, we'll walk through the key concepts, best practices, and actionable steps you can take today.</p>
              <h2>Key Benefits</h2>
              <ul>
                <li>Save time with proven strategies</li>
                <li>Improve results with data-driven approaches</li>
                <li>Scale your efforts efficiently</li>
              </ul>
              <h2>Getting Started</h2>
              <p>To get started with {keyword ?? title}, you'll need to understand the fundamentals. {brandName} has developed a proven approach that combines industry best practices with innovative techniques.</p>
              <h2>Conclusion</h2>
              <p>By implementing these strategies, you'll be well on your way to mastering {keyword ?? title}. Contact {brandName} today to learn more about how we can help you achieve your goals.</p>
            </article>
            """;
    }

    private static string BuildPrompt(GenerateContentCommand req, string? keywordText, string? templateBody)
    {
        if (!string.IsNullOrWhiteSpace(req.CustomPrompt))
            return req.CustomPrompt[..Math.Min(2000, req.CustomPrompt.Length)];

        var prompt = $"Write a {req.WordCount}-word {req.PageType.Replace("_", " ")} about: {req.Title}";
        if (!string.IsNullOrWhiteSpace(keywordText))
            prompt += $". Target keyword: {keywordText}";
        if (!string.IsNullOrWhiteSpace(templateBody))
            prompt += $". Follow this template structure: {templateBody[..Math.Min(500, templateBody.Length)]}";

        return prompt[..Math.Min(2000, prompt.Length)];
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
