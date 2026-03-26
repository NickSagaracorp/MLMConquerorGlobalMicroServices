using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using System.Security.Claims;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.AdminWeb.Services;

/// <summary>
/// Runs server-side: reads ClaimsPrincipal from HttpContext, persists UserInfo to WASM client.
/// </summary>
public class PersistingServerAuthStateProvider : AuthenticationStateProvider, IHostEnvironmentAuthenticationStateProvider, IDisposable
{
    private readonly PersistentComponentState _state;
    private readonly PersistingComponentStateSubscription _subscription;
    private Task<AuthenticationState>? _authStateTask;

    public PersistingServerAuthStateProvider(PersistentComponentState state)
    {
        _state = state;
        _subscription = state.RegisterOnPersisting(PersistAuthStateAsync, RenderMode.InteractiveWebAssembly);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => _authStateTask ?? Task.FromResult(new AuthenticationState(new ClaimsPrincipal()));

    public void SetAuthenticationState(Task<AuthenticationState> task) => _authStateTask = task;

    private async Task PersistAuthStateAsync()
    {
        var state = await GetAuthenticationStateAsync();
        var user  = state.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            _state.PersistAsJson("UserInfo", new UserInfo
            {
                UserId   = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                Email    = user.FindFirstValue(ClaimTypes.Email)          ?? string.Empty,
                MemberId = user.FindFirstValue("member_id")               ?? string.Empty,
                Roles    = user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray()
            });
        }
    }

    public void Dispose() => _subscription.Dispose();
}
