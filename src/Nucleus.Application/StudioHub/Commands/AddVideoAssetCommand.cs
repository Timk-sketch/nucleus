using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.StudioHub.DTOs;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.StudioHub.Commands;

/// <summary>
/// Registers a video asset (YouTube, Vimeo, uploaded file, etc.) for a brand.
/// </summary>
public record AddVideoAssetCommand(
    Guid BrandId,
    string Name,
    string Url,
    string Platform = "other",
    string? ThumbnailUrl = null,
    int? DurationSeconds = null,
    string? Description = null) : IRequest<VideoAssetDto>;

public class AddVideoAssetValidator : AbstractValidator<AddVideoAssetCommand>
{
    public AddVideoAssetValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Url).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Platform).MaximumLength(50);
        RuleFor(x => x.ThumbnailUrl).MaximumLength(1000).When(x => x.ThumbnailUrl != null);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
    }
}

public class AddVideoAssetHandler(INucleusDbContext db, ICurrentTenantService tenant)
    : IRequestHandler<AddVideoAssetCommand, VideoAssetDto>
{
    public async Task<VideoAssetDto> Handle(AddVideoAssetCommand request, CancellationToken cancellationToken)
    {
        var brand = await db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == tenant.TenantId)
            .Select(b => new { b.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null)
            throw new InvalidOperationException("Brand not found for this tenant.");

        var video = new VideoAsset
        {
            TenantId = tenant.TenantId,
            BrandId = request.BrandId,
            Name = request.Name,
            Url = request.Url,
            Platform = request.Platform,
            ThumbnailUrl = request.ThumbnailUrl,
            DurationSeconds = request.DurationSeconds,
            Description = request.Description,
            UploadedAt = DateTimeOffset.UtcNow,
        };

        db.VideoAssets.Add(video);
        await db.SaveChangesAsync(cancellationToken);

        return new VideoAssetDto
        {
            Id = video.Id,
            BrandId = video.BrandId,
            Name = video.Name,
            Url = video.Url,
            ThumbnailUrl = video.ThumbnailUrl,
            DurationSeconds = video.DurationSeconds,
            Platform = video.Platform,
            UploadedAt = video.UploadedAt,
            Description = video.Description,
            CreatedAt = video.CreatedAt,
            UpdatedAt = video.UpdatedAt,
        };
    }
}
