using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace MLMConquerorGlobalEdition.BizCenterWeb.Services;

/// <summary>
/// DelegatingHandler that forwards the member's JWT Bearer token to the
/// BizCenter REST API. Pre-checks JWT expiry to avoid 401 round-trips and,
/// on expiry/401, signs the user out and forces a navigation to
/// /login?error=session_expired so the user sees the login page (with the
/// "Your session has expired" alert) instead of a raw "401 Unauthorized"
/// technical error bubbling out of an in-circuit fetch call.
/// </summary>
public class BizCenterApiAuthHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor        _httpContextAccessor;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly NavigationManager           _nav;

    public BizCenterApiAuthHandler(
        IHttpContextAccessor        httpContextAccessor,
        AuthenticationStateProvider authStateProvider,
        NavigationManager           nav)
    {
        _httpContextAccessor = httpContextAccessor;
        _authStateProvider   = authStateProvider;
        _nav                 = nav;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _httpContextAccessor.HttpContext?.User.FindFirstValue("access_token");

        if (string.IsNullOrEmpty(token))
        {
            try
            {
                var state = await _authStateProvider.GetAuthenticationStateAsync();
                token = state.User.FindFirstValue("access_token");
            }
            catch { /* provider not yet ready — proceed without token */ }
        }

        if (!string.IsNullOrEmpty(token) && IsTokenExpired(token))
        {
            await SignOutAndRedirectAsync();
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            await SignOutAndRedirectAsync();

        return response;
    }

    private async Task SignOutAndRedirectAsync()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null && !ctx.Response.HasStarted)
        {
            try { await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); }
            catch { /* best effort */ }
        }

        if (_authStateProvider is PersistingServerAuthStateProvider persisting)
            persisting.MarkUserAsLoggedOut();

        // Force a full browser navigation to /login. forceLoad bypasses the
        // Blazor router so the auth pipeline re-runs from the fresh request,
        // and the in-flight component never gets a chance to show its raw
        // "401" error to the user. Wrapped in try/catch because static SSR
        // (initial page render before circuit) has no current URI.
        try
        {
            _nav.NavigateTo("/login?error=session_expired", forceLoad: true);
        }
        catch { /* best effort — the cookie clear is what really matters */ }
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
