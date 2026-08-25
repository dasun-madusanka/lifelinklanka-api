using Hangfire.Dashboard;

namespace LifeLinkLanka.API;

public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        // For local/dev only: allow anyone. Replace with role check once you have
        // a cookie-based admin session, since Hangfire's dashboard doesn't easily
        // carry your JWT bearer token from the browser address bar.
        return httpContext.Request.Host.Host is "localhost" or "127.0.0.1";
    }
}