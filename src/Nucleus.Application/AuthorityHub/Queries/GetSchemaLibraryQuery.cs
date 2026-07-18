using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.AuthorityHub.DTOs;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.AuthorityHub.Queries;

/// <summary>
/// Returns the schema template library for a brand, optionally filtered by page_type.
/// Tenant-scoped. Ordered by PageType then SchemaType.
/// </summary>
public record GetSchemaLibraryQuery(
    Guid BrandId,
    string? PageType = null,
    bool ActiveOnly = false) : IRequest<List<SchemaTemplateDto>>;

public class GetSchemaLibraryHandler : IRequestHandler<GetSchemaLibraryQuery, List<SchemaTemplateDto>>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetSchemaLibraryHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<SchemaTemplateDto>> Handle(
        GetSchemaLibraryQuery request, CancellationToken cancellationToken)
    {
        var query = _db.SchemaTemplates
            .Where(s => s.BrandId == request.BrandId);

        if (!string.IsNullOrWhiteSpace(request.PageType))
            query = query.Where(s => s.PageType == request.PageType);

        if (request.ActiveOnly)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.PageType)
            .ThenBy(s => s.SchemaType)
            .Select(s => new SchemaTemplateDto
            {
                Id = s.Id,
                BrandId = s.BrandId,
                PageType = s.PageType,
                SchemaType = s.SchemaType,
                TemplateJson = s.TemplateJson,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
            })
            .ToListAsync(cancellationToken);
    }
}
