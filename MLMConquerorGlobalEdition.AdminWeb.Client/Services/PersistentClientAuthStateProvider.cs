using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace MLMConquerorGlobalEdition.AdminWeb.Client.Services;

/// <summary>
/// Runs in WASM: reads UserInfo persisted by the server during prerender.
/// Uses a local UserInfo mirror to keep WASM client free of ASP.NET Core framework dependencies.
/// </summary>
public class PersistentClientAuthStateProvider : AuthenticationStateProvider
{
    private readonly PersistentComponentState _state;
    private AuthenticationState? _cached;

    public PersistentClientAuthStateProvider(PersistentComponentState state)
    {
        _state = state;
        if (_state.TryTakeFromJson<UserInfo>("UserInfo", out var info) && info is not null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, info.UserId),
                new(ClaimTypes.Email,          info.Email),
                new("member_id",               info.MemberId),
            };
            claims.AddRange(info.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
            _cached = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie")));
        }
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(_cached ?? new AuthenticationState(new ClaimsPrincipal()));

    // Mirrors SharedKernel.UserInfo — local copy keeps WASM client free of ASP.NET Core framework deps.
    private sealed class UserInfo
    {
        public string   UserId   { get; set; } = string.Empty;
        public string   MemberId { get; set; } = string.Empty;
        public string   Email    { get; set; } = string.Empty;
        public string[] Roles    { get; set; } = [];
    }
}
