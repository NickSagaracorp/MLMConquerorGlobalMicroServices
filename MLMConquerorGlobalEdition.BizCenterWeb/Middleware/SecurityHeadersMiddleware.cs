namespace MLMConquerorGlobalEdition.BizCenterWeb.Middleware;

/// <summary>
/// Applies OWASP-recommended security headers for the Blazor web frontend.
/// CSP is tuned for Blazor Server/WASM: allows scripts and styles from self and
/// inline styles required by Blazor's runtime (nonce-based preferred, hash-based fallback).
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent clickjacking
        headers["X-Frame-Options"]        = "DENY";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-XSS-Protection"]       = "1; mode=block";
        headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"]      = "geolocation=(), microphone=(), camera=(), payment=()";

        // CSP for Blazor — allows 'self' scripts/styles; unsafe-inline needed for Blazor runtime
        // TODO: Replace 'unsafe-inline' with nonces once the Blazor CSP nonce middleware is in place.
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
