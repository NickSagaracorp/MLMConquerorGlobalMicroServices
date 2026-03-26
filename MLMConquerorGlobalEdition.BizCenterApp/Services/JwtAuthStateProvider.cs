using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace MLMConquerorGlobalEdition.BizCenterApp.Services;

/// <summary>
/// Reads the stored JWT from SecureStorage and builds the ClaimsPrincipal.
/// Called by Blazor's CascadingAuthenticationState on every render cycle.
/// </summary>
public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private const string AccessTokenKey  = "access_token";
    private const string RefreshTokenKey = "refresh_token";

    private readonly JwtSecurityTokenHandler _handler = new();

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync(AccessTokenKey);
            if (string.IsNullOrWhiteSpace(token))
                return Anonymous();

            if (!_handler.CanReadToken(token))
                return Anonymous();

            var jwt = _handler.ReadJwtToken(token);

            // Reject expired tokens
            if (jwt.ValidTo < DateTime.UtcNow)
            {
                await ClearTokens();
                return Anonymous();
            }

            var identity = new ClaimsIdentity(jwt.Claims, "jwt");
            var user     = new ClaimsPrincipal(identity);
            return new AuthenticationState(user);
        }
        catch
        {
            return Anonymous();
        }
    }

    public async Task NotifyLoginAsync(string accessToken, string refreshToken)
    {
        await SecureStorage.SetAsync(AccessTokenKey, accessToken);
        await SecureStorage.SetAsync(RefreshTokenKey, refreshToken);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task NotifyLogoutAsync()
    {
        await ClearTokens();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous()));
    }

    public Task<string?> GetAccessTokenAsync()
        => SecureStorage.GetAsync(AccessTokenKey);

    public Task<string?> GetRefreshTokenAsync()
        => SecureStorage.GetAsync(RefreshTokenKey);

    private async Task ClearTokens()
    {
        SecureStorage.Remove(AccessTokenKey);
        SecureStorage.Remove(RefreshTokenKey);
        await Task.CompletedTask;
    }

    private static AuthenticationState Anonymous()
        => new(new ClaimsPrincipal(new ClaimsIdentity()));
}
