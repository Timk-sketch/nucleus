using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.LeadsHub.DTOs;

namespace Nucleus.Application.LeadsHub.Queries;

public record GetBrandLeadsQuery(
    Guid BrandId,
    Guid? FinderId = null,
    int Days = 30,
    int Page = 1,
    int PageSize = 50) : IRequest<LeadsPageDto?>;

public class GetBrandLeadsQueryHandler(INucleusDbContext db)
    : IRequestHandler<GetBrandLeadsQuery, LeadsPageDto?>
{
    public async Task<LeadsPageDto?> Handle(GetBrandLeadsQuery request, CancellationToken ct)
    {
        var days     = Math.Clamp(request.Days, 1, 365);
        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var since    = DateTimeOffset.UtcNow.AddDays(-days);

        var brandExists = await db.Brands.AnyAsync(b => b.Id == request.BrandId, ct);
        if (!brandExists) return null;

        // All finders for this brand
        var finderIds = await db.Finders
            .Where(f => f.BrandId == request.BrandId)
            .Select(f => f.Id)
            .ToListAsync(ct);

        var query = db.FinderSessions
            .Where(s => finderIds.Contains(s.FinderId)
                     && s.CreatedAt >= since
                     && s.LeadEmail != null);

        if (request.FinderId.HasValue)
            query = query.Where(s => s.FinderId == request.FinderId.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(db.Finders,
                  s => s.FinderId,
                  f => f.Id,
                  (s, f) => new LeadDto
                  {
                      SessionId   = s.Id,
                      FinderId    = f.Id,
                      FinderName  = f.Name,
                      LeadName    = s.LeadName,
                      LeadEmail   = s.LeadEmail,
                      LeadPhone   = s.LeadPhone,
                      AnswersJson = s.AnswersJson,
                      Converted   = s.Converted,
                      CompletedAt = s.CompletedAt,
                      CreatedAt   = s.CreatedAt,
                  })
            .ToListAsync(ct);

        return new LeadsPageDto
        {
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
            Items      = items,
        };
    }
}
