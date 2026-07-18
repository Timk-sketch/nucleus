using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.StudioHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.StudioHub.Commands;

/// <summary>
/// Generates an AI image via Flux and saves it as a DesignAsset.
/// In production this calls the Flux API (Black Forest Labs) with the prompt
/// and stores the returned image URL. AI usage is tracked per tenant.
/// Plan gate: image_generator = agency+
/// </summary>
public record GenerateImageCommand(
    Guid BrandId,
    string Prompt,
    string? StyleHint = null,
    int Width = 1024,
    int Height = 1024) : IRequest<DesignAssetDto>;

public class GenerateImageValidator : AbstractValidator<GenerateImageCommand>
{
    public GenerateImageValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Prompt).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Width).InclusiveBetween(256, 2048);
        RuleFor(x => x.Height).InclusiveBetween(256, 2048);
        RuleFor(x => x.StyleHint).MaximumLength(200).When(x => x.StyleHint != null);
    }
}

public class GenerateImageHandler : IRequestHandler<GenerateImageCommand, DesignAssetDto>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GenerateImageHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<DesignAssetDto> Handle(GenerateImageCommand request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brand = await _db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId)
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null)
            throw new InvalidOperationException("Brand not found for this tenant.");

        // In production: call Flux API here and get back a real URL.
        // For now, generate a placeholder URL via picsum (deterministic for the prompt hash).
        var promptHash = Math.Abs(request.Prompt.GetHashCode()) % 1000;
        var generatedUrl = $"https://picsum.photos/seed/{promptHash}/{request.Width}/{request.Height}";

        var fullPrompt = string.IsNullOrWhiteSpace(request.StyleHint)
            ? request.Prompt
            : $"{request.Prompt} — Style: {request.StyleHint}";

        var asset = new DesignAsset
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            Name = $"AI Image — {request.Prompt[..Math.Min(50, request.Prompt.Length)]}",
            AssetType = "generated",
            Url = generatedUrl,
            Width = request.Width,
            Height = request.Height,
            MimeType = "image/png",
            PromptUsed = fullPrompt,
            UploadedAt = DateTimeOffset.UtcNow,
        };

        _db.DesignAssets.Add(asset);
        await _db.SaveChangesAsync(cancellationToken);

        return new DesignAssetDto
        {
            Id = asset.Id,
            BrandId = asset.BrandId,
            Name = asset.Name,
            AssetType = asset.AssetType,
            Url = asset.Url,
            Width = asset.Width,
            Height = asset.Height,
            UploadedAt = asset.UploadedAt,
            PromptUsed = asset.PromptUsed,
            MimeType = asset.MimeType,
            CreatedAt = asset.CreatedAt,
            UpdatedAt = asset.UpdatedAt,
        };
    }
}
