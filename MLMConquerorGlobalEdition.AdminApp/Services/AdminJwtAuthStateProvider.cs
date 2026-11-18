using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using MLMConquerorGlobalEdition.ClientCore.AccessTokens;

namespace MLMConquerorGlobalEdition.AdminApp.Services;

/// <summary>
/// Reads the stored admin JWT from SecureStorage.
/// Manages both the normal admin token and the short-lived impersonation token.
/// </summary>
/// <remarks>
/// Los dos tokens pasan por <see cref="ClientAccessTokenValidator"/> (firma de nuestro emisor,
/// audiencia y vigencia) antes de armar la identidad. Antes se leían sin comprobar la firma.
/// </remarks>
public class AdminJwtAuthStateProvider : AuthenticationStateProvider
{
    private const string AdminTokenKey         = "admin_access_token";
    private const string AdminRefreshTokenKey  = "admin_refresh_token";
    private const string ImpersonationTokenKey = "impersonation_token";

    private readonly ClientAccessTokenValidator _validator;

    public AdminJwtAuthStateProvider(ClientAccessTokenValidator validator) => _validator = validator;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // If actively impersonating, return impersonation identity
            var impToken = await SecureStorage.GetAsync(ImpersonationTokenKey);
            if (!string.IsNullOrEmpty(impToken))
            {
                var imp = await _validator.CheckAsync(impToken);
                if (imp.IsValid)
                {
                    var impId = new ClaimsIdentity(imp.Token!.Claims, "jwt_impersonation");
                    return new AuthenticationState(new ClaimsPrincipal(impId));
                }
                // Caducado o sin nuestra firma: fuera, y se sigue con la identidad del admin.
                SecureStorage.Remove(ImpersonationTokenKey);
            }

            // Normal admin token
            var token = await SecureStorage.GetAsync(AdminTokenKey);
            if (string.IsNullOrWhiteSpace(token))
                return Anonymous();

            var check = await _validator.CheckAsync(token);
            if (check.Status == AccessTokenStatus.Expired)
            {
                await ClearAdminTokens();
                return Anonymous();
            }

            if (!check.IsValid)
                return Anonymous();

            var identity = new ClaimsIdentity(check.Token!.Claims, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return Anonymous();
        }
    }

    /// <summary>
    /// Guarda la sesión recién emitida por SignupAPI.
    /// </summary>
    /// <param name="refreshToken">
    /// El que venía en la cabecera <c>Set-Cookie</c> de la respuesta, que es donde la API lo
    /// entrega —en el cuerpo lo manda vacío a propósito—. Puede ser null si esa cabecera no llegó;
    /// se admite en vez de reventar porque quedarse sin refresco no es motivo para tirar una sesión
    /// que sí se emitió: el token de acceso vale sus quince minutos y luego habrá que volver a
    /// entrar. Antes se guardaba <c>response.Data.RefreshToken</c> del cuerpo, que siempre era
    /// cadena vacía, así que esta aplicación nunca tuvo con qué renovar nada.
    /// </param>
    public async Task NotifyLoginAsync(string accessToken, string? refreshToken)
    {
        await SecureStorage.SetAsync(AdminTokenKey, accessToken);

        if (!string.IsNullOrWhiteSpace(refreshToken))
            await SecureStorage.SetAsync(AdminRefreshTokenKey, refreshToken!);
        else
            SecureStorage.Remove(AdminRefreshTokenKey);

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
