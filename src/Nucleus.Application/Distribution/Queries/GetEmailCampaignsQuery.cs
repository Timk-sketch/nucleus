using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.Distribution.DTOs;

namespace Nucleus.Application.Distribution.Queries;

/// <summary>
/// Returns all email campaigns for a brand, ordered by most recent first.
/// </summary>
public record GetEmailCampaignsQuery(Guid BrandId) : IRequest<List<EmailCampaignDto>>;

public class GetEmailCampaignsHandler : IRequestHandler<GetEmailCampaignsQuery, List<EmailCampaignDto>>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetEmailCampaignsHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<EmailCampaignDto>> Handle(
        GetEmailCampaignsQuery request, CancellationToken cancellationToken)
    {
        // Load campaigns with rolled-up message stats
        var campaigns = await _db.EmailCampaigns
            .Where(c => c.BrandId == request.BrandId && c.TenantId == _tenant.TenantId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new EmailCampaignDto
            {
                Id = c.Id,
                BrandId = c.BrandId,
                BrandName = c.Brand.Name,
                Subject = c.Subject,
                Status = c.Status,
                RecipientCount = c.RecipientCount,
                SentAt = c.SentAt,
                CreatedAt = c.CreatedAt,
                // Roll up from messages
                TotalMessages = _db.EmailCampaignMessages.Count(m => m.CampaignId == c.Id),
                TotalOpens = _db.EmailCampaignMessages.Where(m => m.CampaignId == c.Id).Sum(m => m.OpenCount),
                TotalClicks = _db.EmailCampaignMessages.Where(m => m.CampaignId == c.Id).Sum(m => m.ClickCount),
            })
            .ToListAsync(cancellationToken);

        return campaigns;
    }
}
