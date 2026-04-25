namespace Nucleus.Api.Middleware;

// ICurrentTenantService is registered as scoped — resolved once per request.
// The actual resolution is done in CurrentTenantService via IHttpContextAccessor.
// This middleware class is a no-op placeholder; the real work is in the service.
public class TenantMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context) => next(context);
}
