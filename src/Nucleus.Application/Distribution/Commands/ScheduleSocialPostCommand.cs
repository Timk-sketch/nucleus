using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.Distribution.Commands;

/// <summary>
/// Schedules a social media post for a brand. The post is created in "scheduled" status.
/// In production this would be pushed to GHL Social Planner or Vista per the brand's config.
/// Platform values: facebook | instagram | twitter | linkedin | gmb
/// </summary>
public record ScheduleSocialPostCommand(
    Guid BrandId,
    string Platform,
    string Caption,
    DateTimeOffset ScheduledAt,
    string? ImageUrl = null,
    Guid? ContentPageId = null,
    string Provider = "ghl") : IRequest<Guid>;

public class ScheduleSocialPostValidator : AbstractValidator<ScheduleSocialPostCommand>
{
    private static readonly string[] ValidPlatforms =
        ["facebook", "instagram", "twitter", "linkedin", "gmb"];

    public ScheduleSocialPostValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Platform)
            .NotEmpty()
            .Must(p => ValidPlatforms.Contains(p.ToLowerInvariant()))
            .WithMessage($"Platform must be one of: {string.Join(", ", ValidPlatforms)}");
        RuleFor(x => x.Caption)
            .NotEmpty()
            .MaximumLength(2000)
            .WithMessage("Caption must be between 1 and 2000 characters.");
        RuleFor(x => x.ScheduledAt)
            .Must(d => d > DateTimeOffset.UtcNow)
            .WithMessage("ScheduledAt must be in the future.");
    }
}

public class ScheduleSocialPostHandler : IRequestHandler<ScheduleSocialPostCommand, Guid>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public ScheduleSocialPostHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(ScheduleSocialPostCommand request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        var post = new SocialPost
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            ContentPageId = request.ContentPageId,
            Platform = request.Platform.ToLowerInvariant(),
            Caption = request.Caption.Trim(),
            ImageUrl = request.ImageUrl?.Trim(),
            ScheduledAt = request.ScheduledAt,
            Status = "scheduled",
            Provider = request.Provider,
        };

        _db.SocialPosts.Add(post);

        // Append a send log entry for the scheduling event
        _db.SendLogs.Add(new SendLog
        {
            TenantId = _tenant.TenantId,
            BrandId = request.BrandId,
            SocialPostId = post.Id,
            Channel = "social",
            RecipientCount = 1,
            SentAt = DateTimeOffset.UtcNow,
            Provider = request.Provider,
            Status = "sent",
        });

        await _db.SaveChangesAsync(cancellationToken);

        return post.Id;
    }
}
