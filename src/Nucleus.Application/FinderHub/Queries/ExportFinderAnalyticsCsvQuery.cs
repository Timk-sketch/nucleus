using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using System.Text;

namespace Nucleus.Application.FinderHub.Queries;

/// <summary>
/// Exports Finder analytics as a CSV string for download.
/// Columns: Date, Starts, Completions, Conversions, CompletionRate, ConversionRate.
/// Returns null if finder not found for this tenant.
/// </summary>
public record ExportFinderAnalyticsCsvQuery(
    Guid FinderId,
    int Days = 30) : IRequest<(string FileName, string Csv)?>;

public class ExportFinderAnalyticsCsvHandler
    : IRequestHandler<ExportFinderAnalyticsCsvQuery, (string FileName, string Csv)?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public ExportFinderAnalyticsCsvHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<(string FileName, string Csv)?> Handle(
        ExportFinderAnalyticsCsvQuery request, CancellationToken cancellationToken)
    {
        var finder = await _db.Finders
            .FirstOrDefaultAsync(
                f => f.Id == request.FinderId && f.TenantId == _tenant.TenantId,
                cancellationToken);

        if (finder is null)
            return null;

        var days = Math.Clamp(request.Days, 1, 365);
        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days + 1));

        var rows = await _db.FinderAnalytics
            .Where(a => a.FinderId == finder.Id && a.Date >= fromDate)
            .OrderBy(a => a.Date)
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Date,Starts,Completions,Conversions,CompletionRate,ConversionRate");

        foreach (var row in rows)
        {
            var cr = row.Starts > 0
                ? Math.Round((double)row.Completions / row.Starts * 100, 2)
                : 0.0;
            var cvr = row.Completions > 0
                ? Math.Round((double)row.Conversions / row.Completions * 100, 2)
                : 0.0;

            sb.AppendLine($"{row.Date:yyyy-MM-dd},{row.Starts},{row.Completions},{row.Conversions},{cr}%,{cvr}%");
        }

        var slug = finder.Slug.Replace(" ", "-").ToLowerInvariant();
        var fileName = $"finder-analytics-{slug}-{days}d-{DateTime.UtcNow:yyyyMMdd}.csv";

        return (fileName, sb.ToString());
    }
}
