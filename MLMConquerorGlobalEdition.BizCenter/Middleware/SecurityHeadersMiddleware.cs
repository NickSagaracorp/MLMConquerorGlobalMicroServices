namespace MLMConquerorGlobalEdition.BizCenter.Middleware;

/// <summary>
/// Applies OWASP-recommended security headers to every HTTP response.
/// Addresses: Clickjacking (X-Frame-Options), MIME sniffing (X-Content-Type-Options),
/// XSS (Content-Security-Policy), HTTPS enforcement (HSTS), information leakage (Server header).
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent clickjacking
        headers["X-Frame-Options"] = "DENY";

        // Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Disable legacy XSS auditor (modern browsers ignore it, but legacy benefit)
        headers["X-XSS-Protection"] = "1; mode=block";

        // Referrer policy — send origin only for same-site, nothing cross-site
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Restrict browser APIs
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=(), payment=()";

        // Content Security Policy — strict for API; scripts never loaded from this API
        headers["Content-Security-Policy"] =
            "default-src 'none'; frame-ancestors 'none'; form-action 'none'";

        // HSTS — enforced by code here for API layer; web hosts do it via UseHsts()
        // Only add if connection is HTTPS to avoid breaking HTTP redirects
        if (context.Request.IsHttps)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        }

        // Remove headers that expose implementation details
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("Server");

        // No caching of API responses that may contain sensitive data
        if (!headers.ContainsKey("Cache-Control"))
            headers["Cache-Control"] = "no-store";

        await _next(context);
    }
}
