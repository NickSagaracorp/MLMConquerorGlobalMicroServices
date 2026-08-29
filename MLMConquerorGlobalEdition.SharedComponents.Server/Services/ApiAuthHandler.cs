using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// El <c>DelegatingHandler</c> que lleva el JWT del usuario a la API del portal: lo saca de la
/// cookie de sesión, comprueba la caducidad ANTES de gastar una llamada, y cuando la sesión ya no
/// vale cierra la sesión y manda al usuario a su pantalla de login.
/// </summary>
/// <remarks>
/// Era un archivo por portal —<c>AdminApiAuthHandler</c> y <c>BizCenterApiAuthHandler</c>— haciendo
/// exactamente el mismo trabajo, y las dos copias ya se habían separado: la del centro de negocios
/// recibió la navegación forzada al login y la de administración nunca. Al unificar se conserva la
/// versión más completa, la del centro de negocios, y con ella se queda también administración.
///
/// Aviso para quien venga a esto esperando que una sesión caducada mande al login: dentro del
/// circuito hoy NO manda, ni en un portal ni en el otro. La navegación de
/// <c>SignOutAndRedirectAsync</c> muere en su propio catch por cómo se registra este manejador, y
/// ahí abajo está explicado con detalle. Comprobado en caliente en los dos portales.
///
/// Lo único del portal es a DÓNDE se manda al usuario. El código del error (<c>session_expired</c>)
/// no se parametriza: es el mismo contrato en los dos, y las dos pantallas de login ya saben
/// traducirlo al aviso de "tu sesión ha caducado". El nombre del cliente HTTP —<c>AdminApi</c>,
/// <c>BizCenterApi</c>, <c>HelpdeskApi</c>…— tampoco entra aquí: este manejador no lo mira, se lo
/// engancha el <c>Program.cs</c> al cliente que quiera con <c>AddHttpMessageHandler</c>, y por eso
/// el mismo sirve para los tres clientes de administración.
/// </remarks>
public class ApiAuthHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor        _httpContextAccessor;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly NavigationManager           _nav;
    private readonly string                      _loginPage;

    /// <param name="loginPage">
    /// La pantalla de login de ESTE portal (<c>/admin/login</c>, <c>/login</c>…), a la que se manda
    /// al usuario cuando su sesión caduca.
    /// </param>
    public ApiAuthHandler(
        IHttpContextAccessor        httpContextAccessor,
        AuthenticationStateProvider authStateProvider,
        NavigationManager           nav,
        string                      loginPage)
    {
        _httpContextAccessor = httpContextAccessor;
        _authStateProvider   = authStateProvider;
        _nav                 = nav;
        _loginPage           = loginPage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Paso 1 — render en servidor o petición HTTP directa: hay HttpContext.
        var token = _httpContextAccessor.HttpContext?.User.FindFirstValue("access_token");

        // Paso 2 — dentro del circuito SignalR de Blazor Server no hay HttpContext, pero el
        // proveedor de estado sí tiene el ClaimsPrincipal que salió de la cookie del apretón de
        // manos.
        if (string.IsNullOrEmpty(token))
        {
            try
            {
                var state = await _authStateProvider.GetAuthenticationStateAsync();
                token = state.User.FindFirstValue("access_token");
            }
            catch { /* el proveedor todavía no está listo: se sigue sin token */ }
        }

        // Comprobación previa: si el token ya caducó, ni se gasta la llamada.
        if (!string.IsNullOrEmpty(token) && IsTokenExpired(token))
        {
            await SignOutAndRedirectAsync();
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            await SignOutAndRedirectAsync();

        return response;
    }

    private async Task SignOutAndRedirectAsync()
    {
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null && !ctx.Response.HasStarted)
        {
            try { await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); }
            catch { /* al mejor esfuerzo */ }
        }

        if (_authStateProvider is PersistingServerAuthStateProvider persisting)
            persisting.MarkUserAsLoggedOut();

        // Navegación completa del navegador al login. forceLoad se salta el enrutador de Blazor,
        // así la tubería de autenticación vuelve a correr desde una petición limpia y el componente
        // que estaba en vuelo no llega a enseñarle al usuario su "401" crudo. Va en try/catch
        // porque en el render estático inicial, antes de que exista circuito, no hay URI actual.
        //
        // OJO — hoy esta navegación NO llega a ocurrir en ninguno de los dos portales, y no es
        // culpa de este código sino de dónde se construye el manejador. Comprobado en caliente en
        // AdminWeb y en BizCenterWeb: dentro del circuito, este NavigateTo lanza
        // "'RemoteNavigationManager' has not been initialized" y se lo traga el catch. La causa es
        // que IHttpClientFactory arma la cadena de manejadores en un ÁMBITO PROPIO —no el del
        // circuito—, así que el NavigationManager que llega por el constructor no es el de la
        // pantalla del usuario; por lo mismo, el MarkUserAsLoggedOut de arriba avisa a un proveedor
        // que nadie escucha y el SignOutAsync se salta porque ahí HttpContext es el de la conexión
        // WebSocket y su respuesta ya empezó. Arreglarlo es cambiar el cableado de los clientes HTTP
        // en los dos Program.cs, no este archivo.
        try
        {
            _nav.NavigateTo($"{_loginPage}?error=session_expired", forceLoad: true);
        }
        catch { /* al mejor esfuerzo: lo que de verdad importa es haber limpiado la cookie */ }
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
