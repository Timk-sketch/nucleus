using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.Search.Commands;

/// <summary>
/// Evaluates all active search alerts for a brand after a rank check completes.
/// Fires alerts when keyword positions cross configured thresholds.
/// Called internally by KeywordRankJob after saving ranks.
/// </summary>
public record EvaluateAlertsCommand(Guid TenantId, Guid BrandId) : IRequest<int>;

public class EvaluateAlertsHandler : IRequestHandler<EvaluateAlertsCommand, int>
{
    private readonly INucleusDbContext _db;

    public EvaluateAlertsHandler(INucleusDbContext db)
    {
        _db = db;
    }

    public async Task<int> Handle(EvaluateAlertsCommand request, CancellationToken cancellationToken)
    {
        // Load active alerts for this brand (bypass tenant filter with explicit TenantId check)
        var alerts = await _db.SearchAlerts
            .Where(a => a.BrandId == request.BrandId
                     && a.TenantId == request.TenantId
                     && a.IsActive)
            .ToListAsync(cancellationToken);

        if (alerts.Count == 0) return 0;

        int fired = 0;

        foreach (var alert in alerts)
        {
            // Get the two most recent ranks for this keyword
            var recentRanks = await _db.KeywordRanks
                .Where(r => r.KeywordId == alert.KeywordId && r.TenantId == request.TenantId)
                .OrderByDescending(r => r.CheckedAt)
                .Take(2)
                .ToListAsync(cancellationToken);

            if (recentRanks.Count == 0) continue;

            var current = recentRanks[0];
            var previous = recentRanks.Count > 1 ? recentRanks[1] : null;

            bool shouldFire = alert.AlertType switch
            {
                "rank_drop" =>
                    current.Position.HasValue
                    && current.Position.Value > alert.Threshold
                    && (previous?.Position == null || previous.Position.Value <= alert.Threshold),

                "rank_rise" =>
                    current.Position.HasValue
                    && current.Position.Value <= alert.Threshold
                    && (previous?.Position == null || previous.Position.Value > alert.Threshold),

                "out_of_top_10" =>
                    current.Position.HasValue
                    && current.Position.Value > 10
                    && (previous?.Position == null || previous.Position.Value <= 10),

                "entered_top_3" =>
                    current.Position.HasValue
                    && current.Position.Value <= 3
                    && (previous?.Position == null || previous.Position.Value > 3),

                _ => false,
            };

            if (shouldFire)
            {
                alert.TriggeredAt = DateTimeOffset.UtcNow;
                alert.Message = alert.AlertType switch
                {
                    "rank_drop"    => $"Keyword dropped to #{current.Position} (threshold: #{alert.Threshold})",
                    "rank_rise"    => $"Keyword rose to #{current.Position} (threshold: #{alert.Threshold})",
                    "out_of_top_10" => $"Keyword dropped out of top 10 (now #{current.Position})",
                    "entered_top_3" => $"🎉 Keyword entered top 3 (now #{current.Position})",
                    _ => $"Alert triggered at #{current.Position}",
                };
                fired++;
            }
        }

        if (fired > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return fired;
    }
}
