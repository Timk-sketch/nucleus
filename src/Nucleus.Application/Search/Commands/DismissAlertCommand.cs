using MediatR;
using Microsoft.EntityFrameworkCore;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Application.Search.Commands;

/// <summary>
/// Dismisses a search alert — marks IsActive = false so it stops firing.
/// </summary>
public record DismissAlertCommand(Guid AlertId) : IRequest<bool>;

public class DismissAlertHandler : IRequestHandler<DismissAlertCommand, bool>
{
    private readonly INucleusDbContext _db;
    private readonly ICurrentTenantService _tenant;

    public DismissAlertHandler(INucleusDbContext db, ICurrentTenantService tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<bool> Handle(DismissAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _db.SearchAlerts
            .Where(a => a.Id == request.AlertId && a.TenantId == _tenant.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (alert is null) return false;

        alert.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
