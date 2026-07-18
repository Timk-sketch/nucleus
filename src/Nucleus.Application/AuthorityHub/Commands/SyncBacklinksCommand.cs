using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;

namespace Nucleus.Application.AuthorityHub.Commands;

/// <summary>
/// Upserts a batch of backlink records for a brand domain.
/// In production this is called by a Hangfire job that pulls data from DataForSEO Backlinks API.
/// Each backlink is matched by SourceUrl — if it exists it's updated (LastSeenAt, DomainRating, IsActive),
/// otherwise it's inserted.
/// Returns the count of new backlinks added.
/// </summary>
public record SyncBacklinksCommand(
    Guid BrandId,
    List<BacklinkInput> Backlinks) : IRequest<SyncBacklinksResult>;

public record BacklinkInput(
    string SourceUrl,
    string TargetUrl,
    string? AnchorText,
    decimal? DomainRating,
    bool IsActive = true);

public record SyncBacklinksResult(int Added, int Updated, int Total);

public class SyncBacklinksValidator : AbstractValidator<SyncBacklinksCommand>
{
    public SyncBacklinksValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.Backlinks).NotNull();
        RuleForEach(x => x.Backlinks).ChildRules(link =>
        {
            link.RuleFor(l => l.SourceUrl).NotEmpty().MaximumLength(1000);
            link.RuleFor(l => l.TargetUrl).NotEmpty().MaximumLength(1000);
            link.RuleFor(l => l.DomainRating)
                .InclusiveBetween(0, 100)
                .When(l => l.DomainRating.HasValue)
                .WithMessage("DomainRating must be between 0 and 100.");
        });
    }
}

public class SyncBacklinksHandler : IRequestHandler<SyncBacklinksCommand, SyncBacklinksResult>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public SyncBacklinksHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SyncBacklinksResult> Handle(
        SyncBacklinksCommand request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        if (request.Backlinks.Count == 0)
            return new SyncBacklinksResult(0, 0, 0);

        var incomingSourceUrls = request.Backlinks
            .Select(b => b.SourceUrl.Trim())
            .Distinct()
            .ToList();

        // Load existing records matching source URLs for this brand
        var existing = await _db.BacklinkRecords
            .Where(b => b.BrandId == request.BrandId && incomingSourceUrls.Contains(b.SourceUrl))
            .ToListAsync(cancellationToken);

        var existingByUrl = existing.ToDictionary(b => b.SourceUrl, StringComparer.OrdinalIgnoreCase);

        int added = 0, updated = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var input in request.Backlinks)
        {
            var sourceUrl = input.SourceUrl.Trim();

            if (existingByUrl.TryGetValue(sourceUrl, out var record))
            {
                // Update existing
                record.LastSeenAt = now;
                record.DomainRating = input.DomainRating;
                record.AnchorText = input.AnchorText;
                record.IsActive = input.IsActive;
                record.TargetUrl = input.TargetUrl.Trim();
                updated++;
            }
            else
            {
                // Insert new
                var newRecord = new BacklinkRecord
                {
                    TenantId = _tenant.TenantId,
                    BrandId = request.BrandId,
                    SourceUrl = sourceUrl,
                    TargetUrl = input.TargetUrl.Trim(),
                    AnchorText = input.AnchorText,
                    DomainRating = input.DomainRating,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    IsActive = input.IsActive,
                };
                _db.BacklinkRecords.Add(newRecord);
                added++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new SyncBacklinksResult(added, updated, added + updated + existing.Count - updated);
    }
}
