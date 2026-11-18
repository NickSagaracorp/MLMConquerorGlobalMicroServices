using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using MLMConquerorGlobalEdition.ClientCore.AccessTokens;

namespace MLMConquerorGlobalEdition.BizCenterApp.Services;

/// <summary>
/// Reads the stored JWT from SecureStorage and builds the ClaimsPrincipal.
/// Called by Blazor's CascadingAuthenticationState on every render cycle.
/// </summary>
/// <remarks>
/// La identidad solo se arma con un token que valida <see cref="ClientAccessTokenValidator"/>:
/// firma de nuestro emisor, audiencia y vigencia. Antes se leía sin comprobar la firma.
/// </remarks>
public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private const string AccessTokenKey  = "access_token";
    private const string RefreshTokenKey = "refresh_token";

    private readonly ClientAccessTokenValidator _validator;

    public JwtAuthStateProvider(ClientAccessTokenValidator validator) => _validator = validator;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await SecureStorage.GetAsync(AccessTokenKey);
            if (string.IsNullOrWhiteSpace(token))
                return Anonymous();

            var check = await _validator.CheckAsync(token);

            // Reject expired tokens
            if (check.Status == AccessTokenStatus.Expired)
            {
                await ClearTokens();
                return Anonymous();
            }

            if (!check.IsValid)
                return Anonymous();

            var identity = new ClaimsIdentity(check.Token!.Claims, "jwt");
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
