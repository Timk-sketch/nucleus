using Nucleus.Application.Common.Interfaces;
using Nucleus.Domain.Entities;
using Nucleus.Infrastructure.Data;

namespace Nucleus.Infrastructure.Services;

public class AuditService(NucleusDbContext db) : IAuditService
{
    public async Task LogAsync(Guid tenantId, Guid? userId, string action, string entityType,
        string? entityId = null, string? changes = null, CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Changes = changes,
        });
        await db.SaveChangesAsync(ct);
    }
}
