using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.CmsRendererHub.Commands;

/// <summary>
/// Invalidates the PageCache entry for a specific slug within a brand.
/// Sets InvalidatedAt to now — the next public page request will re-render and re-cache.
/// Returns true if a cache entry was found and invalidated; false if no cache entry exists.
/// </summary>
public record InvalidatePageCacheCommand(Guid BrandId, string Slug) : IRequest<bool>;

public class InvalidatePageCacheValidator : AbstractValidator<InvalidatePageCacheCommand>
{
    public InvalidatePageCacheValidator()
    {
        RuleFor(x => x.BrandId).NotEmpty().WithMessage("BrandId is required.");
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(300).WithMessage("Slug is required (max 300 chars).");
    }
}

public class InvalidatePageCacheHandler : IRequestHandler<InvalidatePageCacheCommand, bool>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public InvalidatePageCacheHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<bool> Handle(
        InvalidatePageCacheCommand request, CancellationToken cancellationToken)
    {
        // Ensure brand belongs to this tenant
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId,
                cancellationToken);

        if (!brandExists)
            throw new InvalidOperationException("Brand not found for this tenant.");

        var slug = request.Slug.Trim().ToLowerInvariant();

        var entry = await _db.PageCaches
            .FirstOrDefaultAsync(c => c.BrandId == request.BrandId && c.Slug == slug,
                cancellationToken);

        if (entry is null)
            return false;

        entry.InvalidatedAt = DateTimeOffset.UtcNow;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
