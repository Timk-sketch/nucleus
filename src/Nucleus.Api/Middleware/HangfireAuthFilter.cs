using Hangfire.Dashboard;

namespace Nucleus.Api.Middleware;

public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        // Only allow SuperAdmin in production; allow all in development
        if (httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            return true;

        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("SuperAdmin");
    }
}
