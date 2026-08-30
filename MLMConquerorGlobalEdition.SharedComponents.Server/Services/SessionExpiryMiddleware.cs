using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// En cada navegación del navegador: renueva la sesión si su JWT caducó, pone la cookie al día con
/// los tokens vigentes, y solo si la renovación no sale corta la navegación y manda al login con el
/// aviso.
/// </summary>
/// <remarks>
/// LAS DOS COSAS NUEVAS, y las dos ocurren aquí por el mismo motivo: esta es la ÚNICA de las tres
/// piezas de sesión que tiene delante una respuesta HTTP que todavía no ha empezado, así que es la
/// única que puede reescribir la cookie.
///
///   • RENOVAR EN UNA RECARGA. Un usuario que vuelve después de comer y pulsa F5 tiene el JWT
///     caducado y el circuito ni siquiera existe todavía. Aquí se le renueva antes de que se pinte
///     nada, y aterriza en su pantalla en vez de en el login.
///
///   • REEMITIR LA COOKIE. Cuando quien renovó fue el circuito —o un POST del área de cuenta—, la
///     pareja nueva está en <see cref="PortalSessionTokens"/> y la cookie se quedó con la vieja. Eso
///     NO es cosmético: la API ROTA el refresh token, así que una cookie con el refresco viejo es
///     una cookie con una credencial ya invalidada, y si el proceso se reiniciara antes de ponerla
///     al día esa sesión no podría renovarse nunca más. Por eso se reemite en cuanto hay ocasión, y
///     no solo cuando algo caduca.
///
/// LA MITAD DEL ARREGLO QUE OCURRE FUERA DEL CIRCUITO. Dentro del circuito, quien descubre la
/// caducidad es <see cref="ApiAuthHandler"/> y quien navega es el <c>NavigationManager</c> de la
/// pantalla, alcanzado por <see cref="CircuitServicesAccessor"/>. Pero un circuito recién abierto
/// hace su PRIMER render sin que eso sea actividad entrante, así que si el usuario recarga la página
/// —o entra por un marcador, o pulsa atrás— con el token ya caducado, aquel camino no llega a
/// dispararse y el usuario se come el 401 de la primera carga. Que es justo el caso del informe:
/// <c>/admin/config/countries</c> pintando "Error loading countries: 401 (Unauthorized)".
///
/// Aquí sí hay una respuesta HTTP que todavía no ha empezado, así que se puede hacer lo correcto y
/// completo: limpiar la cookie de sesión y redirigir. Sale más barato que dejar arrancar el
/// circuito para descubrirlo dentro.
///
/// QUÉ NO TOCA, y por qué cada exclusión:
///   • lo que no es una navegación del navegador (sin <c>Accept: text/html</c>): el WebSocket de
///     <c>/_blazor</c>, los recursos de <c>/_framework</c>, el CSS, las llamadas de datos. A un
///     WebSocket no se le redirige, y cortarlo dejaría al usuario con la página muerta y sin aviso;
///   • los endpoints de la puerta y del área de cuenta (<c>/account/...</c>): tienen su propia
///     política y son ellos los que firman y cierran sesiones. Interceptar la salida sería impedir
///     salir;
///   • las pantallas de la puerta (login, segundo factor, enrolamiento): redirigir al login desde el
///     propio login es un bucle.
/// </remarks>
public sealed class SessionExpiryMiddleware
{
    private readonly RequestDelegate                    _next;
    private readonly AuthPortalOptions                  _portal;
    private readonly PortalSessionTokens                _sessionTokens;
    private readonly ILogger<SessionExpiryMiddleware>   _logger;

    public SessionExpiryMiddleware(
        RequestDelegate                  next,
        AuthPortalOptions                portal,
        PortalSessionTokens              sessionTokens,
        ILogger<SessionExpiryMiddleware> logger)
    {
        _next          = next;
        _portal        = portal;
        _sessionTokens = sessionTokens;
        _logger        = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsPortalNavigation(context, _portal))
        {
            // Los de la cookie tal cual, para saber después si hay que reescribirla. Se leen ANTES
            // de renovar: después el almacén ya devolvería la pareja nueva y no habría con qué
            // comparar.
            var enLaCookie = PortalSessionTokens.FromClaims(context.User);

            // Sin claim no hay nada que juzgar: esta sesión no la firmó la puerta de este portal.
            if (enLaCookie is not null)
            {
                var vigentes = await _sessionTokens.EnsureFreshAsync(context.User, context.RequestAborted);

                if (vigentes is null)
                {
                    _logger.LogInformation(
                        "Sesión muerta en {Path}: no se pudo renovar, se cierra y se manda al " +
                        "login con el aviso.",
                        context.Request.Path.Value);

                    await SessionExpiry.SignOutAndRedirectAsync(context, _portal.LoginPage);
                    return;
                }

                // La cookie va por detrás del almacén: o porque acabamos de renovar aquí, o porque
                // quien renovó fue un circuito y allí no se podía reescribir. Aquí sí se puede.
                if (vigentes != enLaCookie && !context.Response.HasStarted)
                    await SessionExpiry.ReissueCookieAsync(context, vigentes);
            }
        }

        await _next(context);
    }

    /// <summary>
    /// ¿Esta petición es una navegación del navegador sobre la que este middleware manda?
    /// </summary>
    /// <remarks>
    /// Público y estático para poder ejercer las exclusiones una a una desde las pruebas: cada una
    /// de ellas, mal puesta, produce un fallo que solo se ve con una sesión caducada de verdad —un
    /// bucle de redirecciones en el login, un usuario que no puede salir, un circuito cortado a
    /// media conexión.
    ///
    /// Aquí solo se decide SI se mira esta petición; qué hacer con la sesión —renovarla, reemitir la
    /// cookie o cerrarla— se decide en <see cref="InvokeAsync"/>, que es donde se puede preguntar a
    /// la API.
    /// </remarks>
    public static bool IsPortalNavigation(HttpContext context, AuthPortalOptions portal)
    {
        if (context.User?.Identity?.IsAuthenticated != true) return false;

        // Solo navegaciones del navegador: un GET que pide una página.
        if (!HttpMethods.IsGet(context.Request.Method)) return false;
        if (!AcceptsHtml(context)) return false;

        return !IsGateOrAccountPath(context.Request.Path, portal);
    }

    /// <summary>
    /// Una navegación del navegador pide <c>text/html</c>. Las llamadas de datos, el WebSocket del
    /// circuito y los recursos estáticos, no.
    /// </summary>
    private static bool AcceptsHtml(HttpContext context)
    {
        foreach (var accept in context.Request.Headers.Accept)
        {
            if (accept is not null &&
                accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Los caminos de la puerta y del área de cuenta, que se gobiernan solos.
    /// </summary>
    private static bool IsGateOrAccountPath(PathString path, AuthPortalOptions portal)
    {
        if (path.StartsWithSegments("/account", StringComparison.OrdinalIgnoreCase)) return true;

        return Matches(path, portal.LoginPage)
            || Matches(path, portal.TwoFactorPage)
            || Matches(path, portal.EnrollAuthenticatorPage);
    }

    private static bool Matches(PathString path, string? page) =>
        !string.IsNullOrWhiteSpace(page) &&
        path.StartsWithSegments(page, StringComparison.OrdinalIgnoreCase);
}
