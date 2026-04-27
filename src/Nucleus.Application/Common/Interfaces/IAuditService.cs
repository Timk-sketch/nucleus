namespace Nucleus.Application.Common.Interfaces;

public interface IAuditService
{
    Task LogAsync(Guid tenantId, Guid? userId, string action, string entityType,
        string? entityId = null, string? changes = null, CancellationToken ct = default);
}
