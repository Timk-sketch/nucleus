using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.ContentHub.DTOs;

namespace Nucleus.Application.ContentHub.Queries;

/// <summary>
/// Returns all active content templates for a brand.
/// Includes both brand-specific templates (BrandId match) and global templates (IsGlobal = true).
/// Tenant-scoped.
/// </summary>
public record GetContentTemplatesQuery(
    Guid BrandId,
    string? PageType = null,
    bool ActiveOnly = true) : IRequest<List<ContentTemplateDto>>;

public class GetContentTemplatesHandler : IRequestHandler<GetContentTemplatesQuery, List<ContentTemplateDto>>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetContentTemplatesHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<ContentTemplateDto>> Handle(
        GetContentTemplatesQuery request, CancellationToken cancellationToken)
    {
        // Verify the brand belongs to this tenant
        var brandExists = await _db.Brands
            .AnyAsync(b => b.Id == request.BrandId && b.TenantId == _tenant.TenantId, cancellationToken);

        if (!brandExists) return [];

        var query = _db.ContentTemplates
            .Where(t => (t.BrandId == request.BrandId || t.IsGlobal));

        if (request.ActiveOnly)
            query = query.Where(t => t.IsActive);

        if (!string.IsNullOrWhiteSpace(request.PageType))
            query = query.Where(t => t.PageType == request.PageType);

        return await query
            .OrderBy(t => t.IsGlobal)  // brand-specific first
            .ThenBy(t => t.Name)
            .Select(t => new ContentTemplateDto
            {
                Id = t.Id,
                BrandId = t.BrandId,
                Name = t.Name,
                PageType = t.PageType,
                Body = t.Body,
                IsGlobal = t.IsGlobal,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt,
            })
            .ToListAsync(cancellationToken);
    }
}
