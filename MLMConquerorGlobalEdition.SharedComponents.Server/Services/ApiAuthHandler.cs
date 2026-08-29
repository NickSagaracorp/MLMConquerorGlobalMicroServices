using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
/// POR QUÉ ESE ARREGLO NO FUNCIONABA, que es lo que este archivo cierra. <c>IHttpClientFactory</c>
/// construye la cadena de manejadores en un ámbito de DI PROPIO y la reutiliza para todas las
/// llamadas de todos los usuarios. Los tres servicios con ámbito que este manejador recibía por el
/// constructor —<c>NavigationManager</c>, <c>AuthenticationStateProvider</c>,
/// <c>IHttpContextAccessor</c>— no eran nunca los del circuito de quien llamaba, así que el
/// <c>NavigateTo</c> lanzaba "'RemoteNavigationManager' has not been initialized", se lo tragaba su
/// propio <c>catch</c>, y el usuario se quedaba mirando el 401 crudo de la llamada en vuelo. Estuvo
/// así desde el principio en los dos portales.
///
/// Ahora los servicios del circuito se piden a <see cref="CircuitServicesAccessor"/>, que es la
/// forma soportada de cruzar esa frontera, y NO se inyecta ninguno con ámbito por el constructor:
/// un servicio inyectado que nunca es el bueno es peor que no tenerlo, porque aparenta funcionar.
///
/// A DÓNDE MANDA. Desde el circuito, a la SALIDA del portal (<c>/account/logout</c>) con
/// <c>?reason=session_expired</c>, no directamente al login. La razón es la tercera consecuencia del
/// fallo original: dentro del circuito la única respuesta HTTP a mano es la del WebSocket y ya
/// empezó, así que la cookie de sesión NO se puede limpiar desde aquí. Pasando por la salida se
/// limpia de verdad —ahí hay una petición nueva— y es la salida la que redirige al login del portal
/// con el aviso. Fuera del circuito, cuando hay una respuesta que todavía no ha empezado, se hace lo
/// mismo directamente.
///
/// LO QUE ESTE MANEJADOR YA NO HACE: <c>MarkUserAsLoggedOut()</c>. Avisar al proveedor de estado
/// dispara la plantilla <c>NotAuthorized</c> del portal, que navega al login POR SU CUENTA y sin el
/// <c>?error=</c>; en carrera con la navegación de aquí, el aviso se perdía la mitad de las veces.
/// La navegación forzada a la salida se lleva por delante el circuito entero, que es lo que se
/// quería.
///
/// Lo único del portal es a DÓNDE se manda al usuario. El código del error (<c>session_expired</c>)
/// no se parametriza: es el mismo contrato en los dos, y las dos pantallas de login ya saben
/// traducirlo. El nombre del cliente HTTP —<c>AdminApi</c>, <c>BizCenterApi</c>,
/// <c>HelpdeskApi</c>…— tampoco entra aquí: este manejador no lo mira, se lo engancha el
/// <c>Program.cs</c> al cliente que quiera con <c>AddHttpMessageHandler</c>, y por eso el mismo
/// sirve para los cuatro clientes de los dos portales.
/// </remarks>
public class ApiAuthHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor      _httpContextAccessor;
    private readonly CircuitServicesAccessor   _circuitServices;
    private readonly ILogger<ApiAuthHandler>   _logger;
    private readonly string                    _loginPage;
    private readonly string                    _logoutRoute;

    /// <param name="loginPage">
    /// La pantalla de login de ESTE portal (<c>/admin/login</c>, <c>/login</c>…). Solo se usa fuera
    /// del circuito, donde se puede redirigir sobre la respuesta en curso.
    /// </param>
    /// <param name="logoutRoute">
    /// La salida de ESTE portal. Es a donde manda el circuito, porque es lo único que puede limpiar
    /// la cookie de sesión desde ahí.
    /// </param>
    public ApiAuthHandler(
        IHttpContextAccessor    httpContextAccessor,
        CircuitServicesAccessor circuitServices,
        ILogger<ApiAuthHandler> logger,
        string                  loginPage,
        string                  logoutRoute)
    {
        _httpContextAccessor = httpContextAccessor;
        _circuitServices     = circuitServices;
        _logger              = logger;
        _loginPage           = loginPage;
        _logoutRoute         = logoutRoute;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Los servicios del circuito de quien llama, si esta llamada sale de uno. Se leen UNA vez y
        // se pasan hacia abajo: son un AsyncLocal y no hay motivo para volver a mirarlo.
        var circuit = _circuitServices.Services;

        var token = await ResolveTokenAsync(circuit);

        // Comprobación previa: si el token ya caducó, ni se gasta la llamada.
        if (!string.IsNullOrEmpty(token) && SessionExpiry.IsExpired(token))
        {
            await EndSessionAsync(circuit);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized) { RequestMessage = request };
        }

        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            await EndSessionAsync(circuit);

        return response;
    }

    /// <summary>
    /// De dónde sale el JWT del usuario, en este orden.
    /// </summary>
    /// <remarks>
    /// Paso 1 — render en servidor o petición HTTP directa: hay <c>HttpContext</c> y el principal de
    /// la cookie está en él.
    ///
    /// Paso 2 — dentro del circuito, donde el <c>HttpContext</c> puede no estar (según desde qué
    /// contexto de ejecución venga la llamada, y las lecturas de un grid de Syncfusion son
    /// exactamente uno de los que no lo tienen). El proveedor de estado del CIRCUITO siempre tiene
    /// el <c>ClaimsPrincipal</c> que salió de la cookie del apretón de manos.
    ///
    /// Ese paso 2 es el que antes preguntaba al proveedor del ámbito de la fábrica, que está vacío,
    /// y devolvía nada. De ahí el 401 que <c>Members.razor</c> esquivaba adjuntando el Bearer a mano
    /// desde el <c>AuthenticationState</c> en cascada; ese apaño ya no hace falta y se ha quitado.
    /// </remarks>
    private async Task<string?> ResolveTokenAsync(IServiceProvider? circuit)
    {
        var token = _httpContextAccessor.HttpContext?.User.FindFirstValue(SessionExpiry.AccessTokenClaim);
        if (!string.IsNullOrEmpty(token)) return token;

        if (circuit is null) return null;

        try
        {
            var provider = circuit.GetService<AuthenticationStateProvider>();
            if (provider is null) return null;

            var state = await provider.GetAuthenticationStateAsync();
            return state.User.FindFirstValue(SessionExpiry.AccessTokenClaim);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo leer el estado de autenticación del circuito.");
            return null;
        }
    }

    /// <summary>
    /// La sesión ya no vale: fuera de aquí y al login con el aviso.
    /// </summary>
    private async Task EndSessionAsync(IServiceProvider? circuit)
    {
        // Camino del circuito: navegación completa del navegador a la salida del portal. forceLoad
        // se salta el enrutador de Blazor, así la cookie se limpia en una petición nueva y el
        // componente que estaba en vuelo no llega a enseñarle al usuario su "401" crudo.
        if (circuit is not null)
        {
            var nav = circuit.GetService<NavigationManager>();
            if (nav is not null)
            {
                try
                {
                    nav.NavigateTo(SessionExpiry.LogoutUrl(_logoutRoute), forceLoad: true);
                    return;
                }
                catch (Exception ex)
                {
                    // Ya no es un catch mudo: si esto vuelve a romperse, queda escrito. El camino
                    // de abajo sigue siendo mejor que nada.
                    _logger.LogWarning(ex,
                        "No se pudo navegar a la salida del portal desde el circuito.");
                }
            }
        }

        // Fuera del circuito: si la respuesta en curso todavía no ha empezado, se puede hacer lo
        // mismo aquí mismo. Si ya empezó, no hay nada que hacer desde un manejador HTTP y la llamada
        // vuelve con su 401; para ese caso está el middleware, que mira la sesión ANTES de renderizar.
        var ctx = _httpContextAccessor.HttpContext;
        if (ctx is not null && !ctx.Response.HasStarted)
        {
            try
            {
                await SessionExpiry.SignOutAndRedirectAsync(ctx, _loginPage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo cerrar la sesión sobre la respuesta en curso.");
            }
        }
    }
}
