using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// Corta cualquier navegación de un usuario cuya sesión ya no vale y lo manda al login con el aviso.
/// </summary>
/// <remarks>
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
    private readonly ILogger<SessionExpiryMiddleware>   _logger;

    public SessionExpiryMiddleware(
        RequestDelegate next, AuthPortalOptions portal, ILogger<SessionExpiryMiddleware> logger)
    {
        _next   = next;
        _portal = portal;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldEndSession(context, _portal))
        {
            _logger.LogInformation(
                "Sesión caducada en {Path}: se cierra y se manda al login con el aviso.",
                context.Request.Path.Value);

            await SessionExpiry.SignOutAndRedirectAsync(context, _portal.LoginPage);
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// ¿Esta petición es una navegación de un usuario con la sesión muerta?
    /// </summary>
    /// <remarks>
    /// Público y estático para poder ejercer las exclusiones una a una desde las pruebas: cada una
    /// de ellas, mal puesta, produce un fallo que solo se ve con una sesión caducada de verdad —un
    /// bucle de redirecciones en el login, un usuario que no puede salir, un circuito cortado a
    /// media conexión.
    /// </remarks>
    public static bool ShouldEndSession(HttpContext context, AuthPortalOptions portal)
    {
        if (context.User?.Identity?.IsAuthenticated != true) return false;

        // Solo navegaciones del navegador: un GET que pide una página.
        if (!HttpMethods.IsGet(context.Request.Method)) return false;
        if (!AcceptsHtml(context)) return false;

        if (IsGateOrAccountPath(context.Request.Path, portal)) return false;

        // Sin claim no hay nada que juzgar: esta sesión no la firmó la puerta de este portal.
        var token = context.User.FindFirst(SessionExpiry.AccessTokenClaim)?.Value;
        if (string.IsNullOrEmpty(token)) return false;

        return SessionExpiry.IsExpired(token);
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
