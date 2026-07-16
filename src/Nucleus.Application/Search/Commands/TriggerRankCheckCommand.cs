using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.Search.Commands;

/// <summary>
/// Triggers an on-demand keyword rank check for a specific brand.
/// Enqueues the actual DataForSEO work as a background job — the command
/// simply validates the brand belongs to the current tenant and signals the job.
/// </summary>
public record TriggerRankCheckCommand(Guid BrandId) : IRequest<TriggerRankCheckResult>;

public record TriggerRankCheckResult(bool Queued, string Message);

public class TriggerRankCheckHandler : IRequestHandler<TriggerRankCheckCommand, TriggerRankCheckResult>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public TriggerRankCheckHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<TriggerRankCheckResult> Handle(
        TriggerRankCheckCommand request, CancellationToken cancellationToken)
    {
        // Verify brand belongs to this tenant
        var brand = await _db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId)
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null)
            return new TriggerRankCheckResult(false, "Brand not found.");

        var keywordCount = await _db.BrandKeywords
            .CountAsync(k => k.BrandId == request.BrandId, cancellationToken);

        if (keywordCount == 0)
            return new TriggerRankCheckResult(false, "No keywords to check for this brand.");

        // The actual background job is enqueued by the controller (Hangfire dependency-free Application layer)
        return new TriggerRankCheckResult(true,
            $"Rank check queued for {brand.Name} ({keywordCount} keywords).");
    }
}
