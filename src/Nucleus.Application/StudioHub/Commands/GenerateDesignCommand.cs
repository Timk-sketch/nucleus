using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.StudioHub.DTOs;

namespace Nucleus.Application.StudioHub.Commands;

/// <summary>
/// Generates an AI-assisted HTML page design for a brand.
/// Uses brand identity context (colors, domain, name) + a user prompt
/// to produce a starter HTML template saved as a draft page.
/// Plan gate: design_studio = pro+
/// </summary>
public record GenerateDesignCommand(
    Guid BrandId,
    string PageType,
    string Prompt,
    string? TargetSlug = null) : IRequest<WebsitePageDto>;

public class GenerateDesignValidator : AbstractValidator<GenerateDesignCommand>
{
    public GenerateDesignValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.PageType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Prompt).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.TargetSlug).MaximumLength(300).When(x => x.TargetSlug != null);
    }
}

public class GenerateDesignHandler : IRequestHandler<GenerateDesignCommand, WebsitePageDto>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;
    private readonly IClaudeService _claude;
    private readonly ITenantPlanService _plan;

    public GenerateDesignHandler(
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

    public async Task<WebsitePageDto> Handle(
        GenerateDesignCommand request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brand = await _db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId)
            .Select(b => new { b.Id, b.Name, b.Domain, b.PrimaryColor })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null)
            throw new InvalidOperationException("Brand not found for this tenant.");

        // Plan gate: design_generation = pro+
        if (!await _plan.IsFeatureAllowedAsync("design_generation", cancellationToken))
            throw new InvalidOperationException("AI design generation requires a Pro or Agency plan.");

        var slug = request.TargetSlug?.Trim().ToLowerInvariant()
            ?? $"generated-{request.PageType.ToLower()}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var systemPrompt = $"""
            You are an expert web designer and frontend developer for {brand.Name}.
            Generate a complete, self-contained HTML page with embedded CSS.
            Brand color: {brand.PrimaryColor ?? "#6366f1"}. Domain: {brand.Domain ?? brand.Name.ToLower().Replace(" ", "") + ".com"}.
            Return ONLY valid HTML starting with <!DOCTYPE html> — no markdown, no explanation.
            The page must be mobile-responsive, visually polished, and production-ready.
            """;

        var userPrompt = $"Create a {request.PageType} page for {brand.Name}. Requirement: {request.Prompt}";

        var html = await _claude.GenerateAsync(
            systemPrompt, userPrompt,
            model: "claude-sonnet-4-6",
            maxTokens: 8000,
            ct: cancellationToken);

        // Enforce slug uniqueness — append timestamp suffix if taken
        var slugTaken = await _db.WebsitePages
            .AnyAsync(p => p.BrandId == request.BrandId && p.Slug == slug, cancellationToken);

        if (slugTaken)
            slug = slug + "-" + DateTime.UtcNow.ToString("HHmmss");

        var page = new Domain.Entities.WebsitePage
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            Slug = slug,
            Title = $"[AI] {request.PageType} — {brand.Name}",
            PageType = request.PageType,
            HtmlContent = html,
            SeoTitle = $"{request.PageType} | {brand.Name}",
            Status = "draft",
        };

        _db.WebsitePages.Add(page);
        await _db.SaveChangesAsync(cancellationToken);

        return new WebsitePageDto
        {
            Id = page.Id,
            BrandId = page.BrandId,
            Slug = page.Slug,
            Title = page.Title,
            PageType = page.PageType,
            HtmlContent = page.HtmlContent,
            SeoTitle = page.SeoTitle,
            Status = page.Status,
            CreatedAt = page.CreatedAt,
            UpdatedAt = page.UpdatedAt,
        };
    }

}
