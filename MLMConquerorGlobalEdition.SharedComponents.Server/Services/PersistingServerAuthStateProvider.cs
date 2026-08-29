using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// El proveedor de estado de autenticación del lado servidor de un portal: lee el
/// <c>ClaimsPrincipal</c> que el alojamiento le entrega desde el <c>HttpContext</c> y persiste el
/// <see cref="UserInfo"/> para que el cliente WebAssembly arranque ya sabiendo quién es el usuario
/// sin una segunda vuelta a la red.
/// </summary>
/// <remarks>
/// Estaba duplicado carácter a carácter en los dos portales; lo único que los distinguía era el
/// nombre de la plantilla de redirección citada en un comentario, que no es una diferencia de
/// comportamiento. Vive en SharedComponents.Server y no en SharedComponents porque
/// <see cref="IHostEnvironmentAuthenticationStateProvider"/> es la puerta por la que el alojamiento
/// WEB empuja el usuario de la petición: en una MAUI no hay quien la empuje, y allí el estado sale
/// del JWT guardado en el dispositivo.
/// </remarks>
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

    /// <summary>
    /// Marca al usuario como desconectado y avisa a los suscriptores, para que
    /// <c>AuthorizeRouteView</c> vuelva a pintar y tome el relevo la plantilla <c>NotAuthorized</c>
    /// del portal, que es la que redirige a su login.
    /// </summary>
    public void MarkUserAsLoggedOut()
    {
        var anonymous = Task.FromResult(new AuthenticationState(new ClaimsPrincipal()));
        _authStateTask = anonymous;
        NotifyAuthenticationStateChanged(anonymous);
    }

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
