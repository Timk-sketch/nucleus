using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.Distribution.DTOs;

namespace Nucleus.Application.Distribution.Queries;

/// <summary>
/// Returns aggregate stats for a specific campaign (open rate, click rate, delivery count).
/// </summary>
public record GetCampaignStatsQuery(Guid CampaignId, Guid BrandId) : IRequest<CampaignStatsDto?>;

public class GetCampaignStatsHandler : IRequestHandler<GetCampaignStatsQuery, CampaignStatsDto?>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetCampaignStatsHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<CampaignStatsDto?> Handle(
        GetCampaignStatsQuery request, CancellationToken cancellationToken)
    {
        var campaign = await _db.EmailCampaigns
            .Where(c => c.Id == request.CampaignId
                     && c.BrandId == request.BrandId
                     && c.TenantId == _tenant.TenantId)
            .Select(c => new
            {
                c.Id,
                c.Subject,
                c.Status,
                c.SentAt,
                c.CreatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (campaign is null) return null;

        var messages = await _db.EmailCampaignMessages
            .Where(m => m.CampaignId == request.CampaignId && m.TenantId == _tenant.TenantId)
            .ToListAsync(cancellationToken);

        return new CampaignStatsDto
        {
            CampaignId = campaign.Id,
            Subject = campaign.Subject,
            Status = campaign.Status,
            TotalMessages = messages.Count,
            TotalRecipients = messages.Sum(m => m.RecipientCount),
            TotalOpens = messages.Sum(m => m.OpenCount),
            TotalClicks = messages.Sum(m => m.ClickCount),
            SentAt = campaign.SentAt,
            CreatedAt = campaign.CreatedAt,
        };
    }
}
