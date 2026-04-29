using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;

namespace MLMConquerorGlobalEdition.BizCenterWeb.Services;

/// <summary>
/// DelegatingHandler that forwards the member's JWT Bearer token to the
/// BizCenter REST API. Pre-checks JWT expiry to avoid 401 round-trips and,
/// on expiry/401, flips auth state to anonymous so AuthorizeRouteView's
/// NotAuthorized template handles the redirect to login.
/// </summary>
public class BizCenterApiAuthHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthenticationStateProvider _authStateProvider;

    public BizCenterApiAuthHandler(
        IHttpContextAccessor httpContextAccessor,
        AuthenticationStateProvider authStateProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authStateProvider   = authStateProvider;
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
