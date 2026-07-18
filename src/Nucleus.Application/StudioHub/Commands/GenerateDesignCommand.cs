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

    public GenerateDesignHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
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

        // Generate a starter HTML template based on brand context + prompt.
        // In production this would call Claude API. For now, produce a rich scaffold.
        var slug = request.TargetSlug?.Trim().ToLowerInvariant()
            ?? $"generated-{request.PageType.ToLower()}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var html = GenerateHtmlScaffold(brand.Name, brand.Domain, brand.PrimaryColor, request.PageType, request.Prompt);

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

    private static string GenerateHtmlScaffold(
        string brandName, string? domain, string? primaryColor, string pageType, string prompt)
    {
        var color = primaryColor ?? "#ec4899";
        var domainDisplay = domain ?? brandName.ToLower().Replace(" ", "") + ".com";
        var year = DateTime.UtcNow.Year;

        var lines = new System.Text.StringBuilder();
        lines.AppendLine("<!DOCTYPE html>");
        lines.AppendLine("<html lang=\"en\">");
        lines.AppendLine("<head>");
        lines.AppendLine("  <meta charset=\"UTF-8\">");
        lines.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        lines.AppendLine($"  <title>{pageType} | {brandName}</title>");
        lines.AppendLine("  <style>");
        lines.AppendLine("    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }");
        lines.AppendLine("    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; color: #1a1a2e; line-height: 1.6; }");
        lines.AppendLine($"    .hero {{ background: linear-gradient(135deg, {color}22 0%, {color}08 100%); padding: 80px 24px; text-align: center; border-bottom: 1px solid {color}33; }}");
        lines.AppendLine("    .hero h1 { font-size: clamp(2rem, 5vw, 3.5rem); font-weight: 800; color: #1a1a2e; margin-bottom: 16px; }");
        lines.AppendLine("    .hero p { font-size: 1.2rem; color: #555; max-width: 600px; margin: 0 auto 32px; }");
        lines.AppendLine($"    .btn {{ display: inline-block; background: {color}; color: #fff; padding: 14px 32px; border-radius: 8px; text-decoration: none; font-weight: 600; font-size: 1rem; transition: opacity 0.2s; }}");
        lines.AppendLine("    .btn:hover { opacity: 0.9; }");
        lines.AppendLine("    .section { padding: 64px 24px; max-width: 1100px; margin: 0 auto; }");
        lines.AppendLine("    .section h2 { font-size: 2rem; font-weight: 700; margin-bottom: 24px; color: #1a1a2e; }");
        lines.AppendLine("    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 24px; margin-top: 32px; }");
        lines.AppendLine("    .card { background: #fff; border: 1px solid #e5e7eb; border-radius: 12px; padding: 28px; box-shadow: 0 2px 8px rgba(0,0,0,0.05); }");
        lines.AppendLine($"    .card h3 {{ font-size: 1.1rem; font-weight: 700; color: {color}; margin-bottom: 10px; }}");
        lines.AppendLine("    .footer { background: #1a1a2e; color: #aaa; text-align: center; padding: 32px 24px; margin-top: 64px; font-size: 0.9rem; }");
        lines.AppendLine("  </style>");
        lines.AppendLine("</head>");
        lines.AppendLine("<body>");
        lines.AppendLine("  <!-- HERO -->");
        lines.AppendLine("  <section class=\"hero\">");
        lines.AppendLine($"    <h1>{brandName}</h1>");
        lines.AppendLine($"    <!-- AI Prompt: {prompt} -->");
        lines.AppendLine($"    <p>Your trusted partner for {pageType.ToLower()} solutions. Serving customers across all 50 states.</p>");
        lines.AppendLine("    <a href=\"#contact\" class=\"btn\">Get Started Today</a>");
        lines.AppendLine("  </section>");
        lines.AppendLine("");
        lines.AppendLine("  <!-- FEATURES / CONTENT -->");
        lines.AppendLine("  <section class=\"section\">");
        lines.AppendLine($"    <h2>Why Choose {brandName}?</h2>");
        lines.AppendLine("    <div class=\"grid\">");
        lines.AppendLine("      <div class=\"card\"><h3>&#9889; Fast &amp; Reliable</h3><p>We deliver results quickly with a process designed to save you time.</p></div>");
        lines.AppendLine("      <div class=\"card\"><h3>&#128737; Trusted Experts</h3><p>Our team has years of experience helping customers navigate complex requirements.</p></div>");
        lines.AppendLine("      <div class=\"card\"><h3>&#128172; Always Here</h3><p>Real support from real people. Reach us by phone, email, or live chat anytime.</p></div>");
        lines.AppendLine("    </div>");
        lines.AppendLine("  </section>");
        lines.AppendLine("");
        lines.AppendLine("  <!-- CTA -->");
        lines.AppendLine("  <section class=\"section\" id=\"contact\" style=\"text-align:center;background:#f9fafb;border-radius:16px;padding:64px;\">");
        lines.AppendLine("    <h2>Ready to Get Started?</h2>");
        lines.AppendLine($"    <p style=\"color:#555;margin-bottom:28px;\">Contact {brandName} today. We'll walk you through every step.</p>");
        lines.AppendLine($"    <a href=\"https://{domainDisplay}/contact\" class=\"btn\">Contact Us</a>");
        lines.AppendLine("  </section>");
        lines.AppendLine("");
        lines.AppendLine("  <!-- FOOTER -->");
        lines.AppendLine("  <footer class=\"footer\">");
        lines.AppendLine($"    <p>&copy; {year} {brandName}. All rights reserved.</p>");
        lines.AppendLine("  </footer>");
        lines.AppendLine("</body>");
        lines.AppendLine("</html>");

        return lines.ToString();
    }
}
