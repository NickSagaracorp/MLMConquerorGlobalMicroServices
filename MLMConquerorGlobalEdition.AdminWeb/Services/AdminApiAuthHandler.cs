using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;

namespace MLMConquerorGlobalEdition.AdminWeb.Services;

public class AdminApiAuthHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider _authStateProvider;

    public AdminApiAuthHandler(
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider authStateProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authStateProvider   = authStateProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Phase 1 — SSR / direct HTTP request: HttpContext is available
        var token = _httpContextAccessor.HttpContext?.User.FindFirstValue("access_token");

        // Phase 2 — Blazor Server SignalR circuit: HttpContext is null, but
        // AuthenticationStateProvider has the ClaimsPrincipal from the WS handshake cookie
        if (string.IsNullOrEmpty(token))
        {
            try
            {
                var state = await _authStateProvider.GetAuthenticationStateAsync();
                token = state.User.FindFirstValue("access_token");
            }
            catch { /* avoid breaking the pipeline if provider not yet ready */ }
        }

        // Pre-check: if token is already expired, skip the API call and force re-login
        if (!string.IsNullOrEmpty(token) && IsTokenExpired(token))
        {
            await SignOutAsync();
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            await SignOutAsync();

        return response;
    }

    /// <summary>
    /// Clears the auth cookie (when SSR HttpContext is available) and flips the
    /// AuthenticationStateProvider to anonymous. The state change triggers
    /// AuthorizeRouteView to render NotAuthorized → RedirectToAdminLogin.
    /// </summary>
    private async Task SignOutAsync()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null && !ctx.Response.HasStarted)
        {
            try { await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); }
            catch { /* best effort */ }
        }

        if (_authStateProvider is PersistingServerAuthStateProvider persisting)
            persisting.MarkUserAsLoggedOut();
    }

    private static bool IsTokenExpired(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token)) return true;
            var jwt = handler.ReadJwtToken(token);
            return jwt.ValidTo <= DateTime.UtcNow.AddSeconds(5);
        }
        catch { return true; }
    }
}
