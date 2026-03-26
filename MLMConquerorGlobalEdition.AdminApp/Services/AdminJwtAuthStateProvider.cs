using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace MLMConquerorGlobalEdition.AdminApp.Services;

/// <summary>
/// Reads the stored admin JWT from SecureStorage.
/// Manages both the normal admin token and the short-lived impersonation token.
/// </summary>
public class AdminJwtAuthStateProvider : AuthenticationStateProvider
{
    private const string AdminTokenKey         = "admin_access_token";
    private const string AdminRefreshTokenKey  = "admin_refresh_token";
    private const string ImpersonationTokenKey = "impersonation_token";

    private readonly JwtSecurityTokenHandler _handler = new();

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // If actively impersonating, return impersonation identity
            var impToken = await SecureStorage.GetAsync(ImpersonationTokenKey);
            if (!string.IsNullOrEmpty(impToken) && _handler.CanReadToken(impToken))
            {
                var impJwt = _handler.ReadJwtToken(impToken);
                if (impJwt.ValidTo > DateTime.UtcNow)
                {
                    var impId = new ClaimsIdentity(impJwt.Claims, "jwt_impersonation");
                    return new AuthenticationState(new ClaimsPrincipal(impId));
                }
                SecureStorage.Remove(ImpersonationTokenKey);
            }

            // Normal admin token
            var token = await SecureStorage.GetAsync(AdminTokenKey);
            if (string.IsNullOrWhiteSpace(token) || !_handler.CanReadToken(token))
                return Anonymous();

            var jwt = _handler.ReadJwtToken(token);
            if (jwt.ValidTo < DateTime.UtcNow)
            {
                await ClearAdminTokens();
                return Anonymous();
            }

            var identity = new ClaimsIdentity(jwt.Claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return Anonymous();
        }
    }

    public async Task NotifyLoginAsync(string accessToken, string refreshToken)
    {
        await SecureStorage.SetAsync(AdminTokenKey, accessToken);
        await SecureStorage.SetAsync(AdminRefreshTokenKey, refreshToken);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task NotifyLogoutAsync()
    {
        await ClearAdminTokens();
        NotifyAuthenticationStateChanged(Task.FromResult(Anonymous()));
    }

    /// <summary>Stores the impersonation token received from AdminAPI.</summary>
    public async Task StartImpersonationAsync(string impersonationToken)
    {
        await SecureStorage.SetAsync(ImpersonationTokenKey, impersonationToken);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>Removes the impersonation token, reverting to the admin's own identity.</summary>
    public async Task StopImpersonationAsync()
    {
        SecureStorage.Remove(ImpersonationTokenKey);
        await Task.CompletedTask;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public bool IsImpersonating
    {
        get
        {
            var raw = SecureStorage.GetAsync(ImpersonationTokenKey).GetAwaiter().GetResult();
            return !string.IsNullOrEmpty(raw);
        }
    }

    public Task<string?> GetEffectiveTokenAsync()
    {
        var imp = SecureStorage.GetAsync(ImpersonationTokenKey).GetAwaiter().GetResult();
        return !string.IsNullOrEmpty(imp)
            ? Task.FromResult<string?>(imp)
            : SecureStorage.GetAsync(AdminTokenKey);
    }

    public Task<string?> GetAdminTokenAsync()
        => SecureStorage.GetAsync(AdminTokenKey);

    public Task<string?> GetRefreshTokenAsync()
        => SecureStorage.GetAsync(AdminRefreshTokenKey);

    private async Task ClearAdminTokens()
    {
        SecureStorage.Remove(AdminTokenKey);
        SecureStorage.Remove(AdminRefreshTokenKey);
        SecureStorage.Remove(ImpersonationTokenKey);
        await Task.CompletedTask;
    }

    private static AuthenticationState Anonymous()
        => new(new ClaimsPrincipal(new ClaimsIdentity()));
}
