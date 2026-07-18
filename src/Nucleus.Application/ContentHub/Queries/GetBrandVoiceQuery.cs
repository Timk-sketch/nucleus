using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ContentHub.DTOs;

namespace Nucleus.Application.ContentHub.Queries;

/// <summary>
/// Returns the Brand Voice configuration for a brand:
/// the full list of banned words/phrases that the AI generator must avoid.
/// Tenant-scoped.
/// </summary>
public record GetBrandVoiceQuery(Guid BrandId) : IRequest<BrandVoiceDto?>;

public class GetBrandVoiceHandler : IRequestHandler<GetBrandVoiceQuery, BrandVoiceDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetBrandVoiceHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<BrandVoiceDto?> Handle(
        GetBrandVoiceQuery request, CancellationToken cancellationToken)
    {
        var brand = await _db.Brands
            .Where(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId)
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (brand is null) return null;

        var bannedWords = await _db.BannedWords
            .Where(w => w.BrandId == request.BrandId)
            .OrderBy(w => w.Word)
            .Select(w => new BannedWordDto
            {
                Id = w.Id,
                BrandId = w.BrandId,
                Word = w.Word,
                Reason = w.Reason,
                CreatedAt = w.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return new BrandVoiceDto
        {
            BrandId = brand.Id,
            BrandName = brand.Name,
            BannedWords = bannedWords,
            TotalBannedWords = bannedWords.Count,
        };
    }
}
