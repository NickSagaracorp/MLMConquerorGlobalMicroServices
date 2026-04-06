using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace MLMConquerorGlobalEdition.BizCenterWeb.Services;

/// <summary>
/// DelegatingHandler that forwards the member's JWT Bearer token to the
/// BizCenter REST API. Works in both SSR (HttpContext available) and
/// Blazor Server SignalR circuit contexts.
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
        // Phase 1 — SSR: read token claim stored on the ClaimsPrincipal during login
        var token = _httpContextAccessor.HttpContext?.User.FindFirstValue("access_token");

        // Phase 2 — Blazor Server circuit: HttpContext is null; use auth state provider
        if (string.IsNullOrEmpty(token))
        {
            try
            {
                var state = await _authStateProvider.GetAuthenticationStateAsync();
                token = state.User.FindFirstValue("access_token");
            }
            catch { /* provider not yet ready — proceed without token */ }
        }

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
