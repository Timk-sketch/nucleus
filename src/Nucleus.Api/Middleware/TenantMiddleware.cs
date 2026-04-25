using System.Security.Claims;
using Nucleus.Application.Common.Interfaces;

namespace Nucleus.Api.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Resolve tenant from JWT claims and register as scoped service
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantId = context.User.FindFirstValue("tenant_id");
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? context.User.FindFirstValue("sub");
            var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

            if (tenantId != null && userId != null)
            {
                var tenantService = new CurrentTenantService(
                    Guid.Parse(tenantId),
                    Guid.Parse(userId),
                    roles);
                context.RequestServices.GetRequiredService<ICurrentTenantServiceSetter>()
                       .Set(tenantService);
            }
        }

        await _next(context);
    }
}

// Scoped holder that TenantMiddleware writes to
public interface ICurrentTenantServiceSetter
{
    void Set(ICurrentTenantService service);
}

public class CurrentTenantServiceHolder : ICurrentTenantService, ICurrentTenantServiceSetter
{
    private ICurrentTenantService? _inner;

    public Guid TenantId => _inner?.TenantId ?? Guid.Empty;
    public Guid UserId => _inner?.UserId ?? Guid.Empty;
    public string[] Roles => _inner?.Roles ?? Array.Empty<string>();
    public bool IsAuthenticated => _inner is not null;

    public void Set(ICurrentTenantService service) => _inner = service;
}

public class CurrentTenantService : ICurrentTenantService
{
    public Guid TenantId { get; }
    public Guid UserId { get; }
    public string[] Roles { get; }
    public bool IsAuthenticated => true;

    public CurrentTenantService(Guid tenantId, Guid userId, string[] roles)
    {
        TenantId = tenantId;
        UserId = userId;
        Roles = roles;
    }
}
