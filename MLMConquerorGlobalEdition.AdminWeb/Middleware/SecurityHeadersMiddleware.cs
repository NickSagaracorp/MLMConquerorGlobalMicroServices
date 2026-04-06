namespace MLMConquerorGlobalEdition.AdminWeb.Middleware;

/// <summary>
/// Applies OWASP-recommended security headers for the Admin Blazor web frontend.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Frame-Options"]        = "DENY";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-XSS-Protection"]       = "1; mode=block";
        headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"]      = "geolocation=(), microphone=(), camera=(), payment=()";

        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob: https:; " +
            "connect-src 'self' wss: ws:; " +
            "font-src 'self' https:; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "frame-ancestors 'none'";

        if (context.Request.IsHttps)
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";

        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("Server");

        await _next(context);
    }
}
