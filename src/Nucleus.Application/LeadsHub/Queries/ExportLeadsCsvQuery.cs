using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using System.Text;

namespace Nucleus.Application.LeadsHub.Queries;

public record ExportLeadsCsvQuery(Guid BrandId, Guid? FinderId = null, int Days = 30)
    : IRequest<(string FileName, string Csv)?>;

public class ExportLeadsCsvQueryHandler(INucleusDbContext db)
    : IRequestHandler<ExportLeadsCsvQuery, (string FileName, string Csv)?>
{
    public async Task<(string FileName, string Csv)?> Handle(ExportLeadsCsvQuery request, CancellationToken ct)
    {
        var days  = Math.Clamp(request.Days, 1, 365);
        var since = DateTimeOffset.UtcNow.AddDays(-days);

        var brandExists = await db.Brands.AnyAsync(b => b.Id == request.BrandId, ct);
        if (!brandExists) return null;

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

        var rows = await query
            .OrderByDescending(s => s.CreatedAt)
            .Join(db.Finders,
                  s => s.FinderId,
                  f => f.Id,
                  (s, f) => new
                  {
                      Finder      = f.Name,
                      Name        = s.LeadName ?? "",
                      Email       = s.LeadEmail ?? "",
                      Phone       = s.LeadPhone ?? "",
                      Converted   = s.Converted,
                      CreatedAt   = s.CreatedAt,
                  })
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Finder,Name,Email,Phone,Converted,CapturedAt");
        foreach (var r in rows)
        {
            sb.AppendLine($"{Csv(r.Finder)},{Csv(r.Name)},{Csv(r.Email)},{Csv(r.Phone)},{r.Converted},{r.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        }

        var fileName = $"leads-{days}d-{DateTime.UtcNow:yyyyMMdd}.csv";
        return (fileName, sb.ToString());
    }

    private static string Csv(string val) =>
        val.Contains(',') || val.Contains('"') || val.Contains('\n')
            ? $"\"{val.Replace("\"", "\"\"")}\""
            : val;
}
