using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;
using Nucleus.Application.Distribution.DTOs;

namespace Nucleus.Application.Distribution.Queries;

/// <summary>
/// Returns the send log for a brand (email + social sends), ordered newest first.
/// </summary>
public record GetSendLogQuery(
    Guid BrandId,
    int Page = 1,
    int PageSize = 50,
    string? ChannelFilter = null) : IRequest<SendLogPageDto>;

public class SendLogPageDto
{
    public List<SendLogDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class GetSendLogHandler : IRequestHandler<GetSendLogQuery, SendLogPageDto>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public GetSendLogHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<SendLogPageDto> Handle(
        GetSendLogQuery request, CancellationToken cancellationToken)
    {
        var query = _db.SendLogs
            .Where(l => l.BrandId == request.BrandId && l.TenantId == _tenant.TenantId);

        if (!string.IsNullOrWhiteSpace(request.ChannelFilter))
            query = query.Where(l => l.Channel == request.ChannelFilter);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.SentAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => new SendLogDto
            {
                Id = l.Id,
                BrandId = l.BrandId,
                BrandName = l.Brand.Name,
                CampaignId = l.CampaignId,
                SocialPostId = l.SocialPostId,
                Channel = l.Channel,
                RecipientCount = l.RecipientCount,
                SentAt = l.SentAt,
                Provider = l.Provider,
                Status = l.Status,
                ErrorMessage = l.ErrorMessage,
            })
            .ToListAsync(cancellationToken);

        return new SendLogPageDto
        {
            Items = items,
            TotalCount = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
