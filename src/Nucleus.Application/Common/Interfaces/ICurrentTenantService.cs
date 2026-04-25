namespace Nucleus.Application.Common.Interfaces;

public interface ICurrentTenantService
{
    Guid TenantId { get; }
    Guid UserId { get; }
    string[] Roles { get; }
    bool IsAuthenticated { get; }
}
