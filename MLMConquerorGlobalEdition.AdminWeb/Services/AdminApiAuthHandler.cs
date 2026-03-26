using System.Net.Http.Headers;
using System.Security.Claims;
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

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
