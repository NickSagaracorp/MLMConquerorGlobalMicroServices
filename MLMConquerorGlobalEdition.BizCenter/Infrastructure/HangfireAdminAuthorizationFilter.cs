using Hangfire.Dashboard;

namespace MLMConquerorGlobalEdition.BizCenter.Infrastructure;

/// <summary>
/// Restricts Hangfire dashboard access to requests that carry a valid JWT
/// with the "Admin" or "SuperAdmin" role. In development, also allows local requests.
/// </summary>
public class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Allow in development without auth (useful for local debugging)
        if (httpContext.RequestServices
            .GetService<IWebHostEnvironment>()?.IsDevelopment() == true)
            return true;

        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
            return false;

        return httpContext.User.IsInRole("Admin")
            || httpContext.User.IsInRole("SuperAdmin");
    }
}
