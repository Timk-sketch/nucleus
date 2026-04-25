using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Infrastructure.Multitenancy;

public class CurrentTenantService(IHttpContextAccessor httpContextAccessor) : ICurrentTenantService
{
    private readonly ClaimsPrincipal? _user = httpContextAccessor.HttpContext?.User;

    public Guid TenantId =>
        Guid.TryParse(_user?.FindFirstValue("tenant_id"), out var tid) ? tid : Guid.Empty;

    public Guid UserId =>
        Guid.TryParse(_user?.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) ? uid : Guid.Empty;

    public string[] Roles =>
        _user?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];

    public bool IsAuthenticated => _user?.Identity?.IsAuthenticated == true;
}
