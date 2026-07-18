using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.StudioHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.StudioHub.Commands;

/// <summary>
/// Registers an asset (image, document, video, etc.) in the Design Asset library.
/// The caller is responsible for uploading the file to storage — this command
/// records the metadata and URL in the database.
/// Plan gate: asset_library = pro+
/// </summary>
public record UploadAssetCommand(
    Guid BrandId,
    string Name,
    string AssetType,
    string Url,
    int? Width = null,
    int? Height = null,
    long? FileSize = null,
    string? MimeType = null,
    string? PromptUsed = null) : IRequest<DesignAssetDto>;

public class UploadAssetValidator : AbstractValidator<UploadAssetCommand>
{
    private static readonly HashSet<string> ValidTypes =
    [
        "image", "document", "font", "svg", "generated", "other"
    ];

    public UploadAssetValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.AssetType)
            .Must(t => ValidTypes.Contains(t))
            .WithMessage($"AssetType must be one of: {string.Join(", ", ValidTypes)}.");
        RuleFor(x => x.Url).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Width).GreaterThan(0).When(x => x.Width.HasValue);
        RuleFor(x => x.Height).GreaterThan(0).When(x => x.Height.HasValue);
        RuleFor(x => x.FileSize).GreaterThan(0).When(x => x.FileSize.HasValue);
        RuleFor(x => x.MimeType).MaximumLength(100).When(x => x.MimeType != null);
        RuleFor(x => x.PromptUsed).MaximumLength(2000).When(x => x.PromptUsed != null);
    }
}

public class UploadAssetHandler : IRequestHandler<UploadAssetCommand, DesignAssetDto>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public UploadAssetHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<DesignAssetDto> Handle(UploadAssetCommand request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        var asset = new DesignAsset
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            Name = request.Name.Trim(),
            AssetType = request.AssetType,
            Url = request.Url.Trim(),
            Width = request.Width,
            Height = request.Height,
            FileSize = request.FileSize,
            MimeType = request.MimeType,
            PromptUsed = request.PromptUsed,
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
            FileSize = asset.FileSize,
            UploadedAt = asset.UploadedAt,
            PromptUsed = asset.PromptUsed,
            MimeType = asset.MimeType,
            CreatedAt = asset.CreatedAt,
            UpdatedAt = asset.UpdatedAt,
        };
    }
}
