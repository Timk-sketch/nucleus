using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.AuthorityHub.Commands;

/// <summary>
/// Creates a new JSON-LD schema template for a brand + page type.
/// If templateJson is null or empty, a canonical default template is generated
/// based on the pageType (FAQPage, HowTo, Article, Service, LocalBusiness).
/// Returns the new template Id.
/// </summary>
public record CreateSchemaTemplateCommand(
    Guid BrandId,
    string PageType,
    string SchemaType,
    string? TemplateJson = null,
    bool IsActive = true) : IRequest<Guid>;

public class CreateSchemaTemplateValidator : AbstractValidator<CreateSchemaTemplateCommand>
{
    private static readonly string[] ValidPageTypes =
        ["Article", "FAQPage", "HowTo", "Service", "LocalBusiness", "Product", "WebPage"];

    public CreateSchemaTemplateValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.PageType)
            .NotEmpty()
            .Must(p => ValidPageTypes.Contains(p))
            .WithMessage($"PageType must be one of: {string.Join(", ", ValidPageTypes)}");
        RuleFor(x => x.SchemaType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.TemplateJson)
            .Must(json => IsValidJson(json))
            .When(x => !string.IsNullOrWhiteSpace(x.TemplateJson))
            .WithMessage("TemplateJson must be valid JSON.");
    }

    private static bool IsValidJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return true;
        try { JsonDocument.Parse(json); return true; }
        catch { return false; }
    }
}

public class CreateSchemaTemplateHandler : IRequestHandler<CreateSchemaTemplateCommand, Guid>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public CreateSchemaTemplateHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(
        CreateSchemaTemplateCommand request, CancellationToken cancellationToken)
    {
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        var templateJson = string.IsNullOrWhiteSpace(request.TemplateJson)
            ? GenerateDefaultTemplate(request.PageType, request.SchemaType)
            : request.TemplateJson;

        var template = new SchemaTemplate
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            PageType = request.PageType,
            SchemaType = request.SchemaType,
            TemplateJson = templateJson,
            IsActive = request.IsActive,
        };

        _db.SchemaTemplates.Add(template);
        await _db.SaveChangesAsync(cancellationToken);

        return template.Id;
    }

    /// <summary>
    /// Generates a canonical JSON-LD starter template for each supported page type.
    /// Uses {{token}} placeholders for runtime substitution.
    /// </summary>
    private static string GenerateDefaultTemplate(string pageType, string schemaType)
    {
        return pageType switch
        {
            "FAQPage" => JsonSerializer.Serialize(new
            {
                context = "https://schema.org",
                type = "FAQPage",
                mainEntity = new[]
                {
                    new
                    {
                        type = "Question",
                        name = "{{question_1}}",
                        acceptedAnswer = new { type = "Answer", text = "{{answer_1}}" }
                    }
                }
            }, new JsonSerializerOptions { WriteIndented = true })
            .Replace("\"context\"", "\"@context\"")
            .Replace("\"type\"", "\"@type\""),

            "HowTo" => JsonSerializer.Serialize(new
            {
                context = "https://schema.org",
                type = "HowTo",
                name = "{{title}}",
                description = "{{description}}",
                step = new[]
                {
                    new { type = "HowToStep", name = "{{step_1_name}}", text = "{{step_1_text}}" }
                }
            }, new JsonSerializerOptions { WriteIndented = true })
            .Replace("\"context\"", "\"@context\"")
            .Replace("\"type\"", "\"@type\""),

            "Article" => JsonSerializer.Serialize(new
            {
                context = "https://schema.org",
                type = "Article",
                headline = "{{title}}",
                description = "{{description}}",
                author = new { type = "Person", name = "{{author_name}}" },
                datePublished = "{{published_date}}",
                dateModified = "{{modified_date}}"
            }, new JsonSerializerOptions { WriteIndented = true })
            .Replace("\"context\"", "\"@context\"")
            .Replace("\"type\"", "\"@type\""),

            "Service" => JsonSerializer.Serialize(new
            {
                context = "https://schema.org",
                type = "Service",
                name = "{{service_name}}",
                description = "{{description}}",
                provider = new { type = "Organization", name = "{{business_name}}" },
                areaServed = "{{area_served}}"
            }, new JsonSerializerOptions { WriteIndented = true })
            .Replace("\"context\"", "\"@context\"")
            .Replace("\"type\"", "\"@type\""),

            "LocalBusiness" => JsonSerializer.Serialize(new
            {
                context = "https://schema.org",
                type = "LocalBusiness",
                name = "{{business_name}}",
                description = "{{description}}",
                telephone = "{{phone}}",
                address = new
                {
                    type = "PostalAddress",
                    streetAddress = "{{street}}",
                    addressLocality = "{{city}}",
                    addressRegion = "{{state}}",
                    postalCode = "{{zip}}",
                    addressCountry = "US"
                }
            }, new JsonSerializerOptions { WriteIndented = true })
            .Replace("\"context\"", "\"@context\"")
            .Replace("\"type\"", "\"@type\""),

            _ => JsonSerializer.Serialize(new
            {
                context = "https://schema.org",
                type = schemaType,
                name = "{{title}}",
                description = "{{description}}"
            }, new JsonSerializerOptions { WriteIndented = true })
            .Replace("\"context\"", "\"@context\"")
            .Replace("\"type\"", "\"@type\""),
        };
    }
}
