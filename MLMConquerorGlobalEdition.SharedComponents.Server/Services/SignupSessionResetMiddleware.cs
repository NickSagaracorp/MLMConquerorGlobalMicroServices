using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.ClientCore;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// CARGAR LA PANTALLA DE ALTA MATA CUALQUIER SESIÓN ABIERTA EN ESE NAVEGADOR.
/// </summary>
/// <remarks>
/// EL ESCENARIO, que es real y ocurre en cada evento: en un mismo ordenador se dan de alta varias
/// personas seguidas. La persona A termina y se levanta; la persona B se sienta y abre el alta —casi
/// siempre por el enlace del sitio replicado de su patrocinador, que es la ruta con slug—. Si la
/// sesión de A sigue viva en ese navegador, a B le basta con escribir cualquier dirección del portal
/// para estar DENTRO de la cuenta de A: su genealogía, sus comisiones, sus datos personales. Nadie
/// tiene que hacer nada malo para que pase; basta con que A no se acuerde de salir.
///
/// La regla, en palabras del dueño del producto: <em>«nunca desde una sesión iniciada. De hecho si
/// carga la página de signup debe morir cualquier sesión abierta en esa computadora.»</em>
///
/// POR QUÉ ESTO ES UN MIDDLEWARE Y NO UN <c>OnInitializedAsync</c> EN LA PÁGINA. Es la misma pared
/// contra la que ya se dieron el arreglo de la sesión caducada y el del refresco: dentro de un
/// circuito de Blazor Server la respuesta HTTP que hay a mano es la del WebSocket y YA EMPEZÓ
/// (<c>Response.HasStarted</c>), así que desde ahí no se puede borrar una cookie. Un
/// <c>SignOutAsync</c> en la página compilaría, no lanzaría nada visible y dejaría la cookie de A
/// intacta en el navegador de B — que es exactamente el fallo que esto viene a cerrar, pero con una
/// línea de código encima que hace creer que está resuelto. Aquí, en cambio, hay una respuesta que
/// todavía no ha empezado, y es el único sitio donde el borrado es real.
///
/// EN QUÉ SE PARECE Y EN QUÉ NO A <see cref="SessionExpiryMiddleware"/>. El patrón es el mismo
/// —mirar solo navegaciones del navegador, actuar antes de que la respuesta empiece—, pero la
/// condición es la contraria y hay que verlo:
///
///   • Aquel actúa sobre sesiones MUERTAS y las manda al login. Este actúa sobre sesiones VIVAS, que
///     es justo lo peligroso, y NO manda al login: quien abrió el alta quiere darse de alta, y
///     echarle a la puerta de entrada sería cerrarle la única pantalla a la que venía.
///   • Aquel exige el claim del token —sin él no hay nada que juzgar—. Este NO exige nada más que
///     estar autenticado: da igual quién firmara la cookie ni si su token se puede leer, porque lo
///     que hay que garantizar es que después de esto NO QUEDE NINGUNA sesión, no que se juzgue bien
///     una en concreto.
///
/// EL ORDEN EN LA TUBERÍA IMPORTA, y es la parte que se puede romper sin que nada falle al compilar:
/// esto va DESPUÉS de <c>UseAuthentication()</c> —hace falta el <c>ClaimsPrincipal</c> de la cookie—
/// y ANTES de <c>UseSessionExpiry()</c>. Al revés, una persona que llega al alta con el JWT ya
/// caducado se encontraría primero con el middleware de caducidad, que la mandaría a
/// <c>/login?error=session_expired</c>: la sesión moriría igual, sí, pero la persona acabaría en la
/// pantalla de login en vez de en el alta. Como este deja al usuario anónimo antes de ceder el paso,
/// el de caducidad ya no ve nada que juzgar y no se mete.
/// </remarks>
public sealed class SignupSessionResetMiddleware
{
    /// <summary>
    /// La marca con la que la pantalla de alta sabe que acaba de cerrarse una sesión y puede
    /// decirlo.
    /// </summary>
    /// <remarks>
    /// POR QUÉ SE AVISA. A quien va al alta a propósito esto no le sorprende. A quien llega por
    /// error —un miembro que pulsa el enlace del sitio replicado de un compañero para ver cómo se
    /// ve— se le cierra la sesión sin haber pedido nada, y sin una línea que lo explique lo vivirá
    /// como que el portal se ha estropeado. Un aviso discreto convierte un fallo aparente en una
    /// medida de seguridad entendible, y no le quita ni un paso a quien sí venía a darse de alta.
    ///
    /// POR QUÉ VIAJA EN LA URL Y NO EN OTRO SITIO. La página se pinta en un circuito, que es una
    /// petición distinta de esta: <c>HttpContext.Items</c> no llega, y una cookie para decir algo que
    /// se lee una sola vez es basura que hay que acordarse de borrar. La URL es lo único que las dos
    /// peticiones comparten. No es un dato de nadie —un uno— y por eso puede ir a la vista.
    /// </remarks>
    public const string ClosedQueryParam = "session_closed";

    /// <summary>El único valor que la pantalla reconoce.</summary>
    public const string ClosedQueryValue = "1";

    private readonly RequestDelegate                            _next;
    private readonly AuthPortalOptions                          _portal;
    private readonly ChallengeCookieNames                       _challengeCookies;
    private readonly PortalSessionTokens                        _sessionTokens;
    private readonly ILogger<SignupSessionResetMiddleware>      _logger;

    public SignupSessionResetMiddleware(
        RequestDelegate                        next,
        AuthPortalOptions                      portal,
        ChallengeCookieNames                   challengeCookies,
        PortalSessionTokens                    sessionTokens,
        ILogger<SignupSessionResetMiddleware>  logger)
    {
        _next             = next;
        _portal           = portal;
        _challengeCookies = challengeCookies;
        _sessionTokens    = sessionTokens;
        _logger           = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsSignupNavigation(context, _portal.SignupPage) && !context.Response.HasStarted)
        {
            // El gateway es de ámbito de petición —su proveedor de token lee de ESTE HttpContext—,
            // así que sale de aquí y no del constructor: un middleware se construye una vez.
            var api = context.RequestServices.GetRequiredService<AuthApiGateway>();

            var habiaSesion = await PortalSignOut.KillAsync(
                context, api, _challengeCookies, _sessionTokens, context.RequestAborted);

            if (habiaSesion)
            {
                _logger.LogInformation(
                    "Alta abierta en {Path} con una sesión viva en el navegador: se cierra antes " +
                    "de pintar nada.",
                    context.Request.Path.Value);

                // Ya avisada: seguir sin redirigir. Es lo que hace imposible un bucle, pase lo que
                // pase con la cookie al otro lado.
                if (!YaLlevaElAviso(context.Request))
                {
                    context.Response.Redirect(ConAviso(context.Request));
                    return;
                }
            }
        }

        await _next(context);
    }

    /// <summary>
    /// ¿Esta petición es alguien ABRIENDO la pantalla de alta con una sesión encima?
    /// </summary>
    /// <remarks>
    /// Público y estático para poder ejercer cada condición por separado desde las pruebas, igual
    /// que en <see cref="SessionExpiryMiddleware.IsPortalNavigation"/>: las cuatro, mal puestas,
    /// producen fallos que solo se ven con una sesión de verdad en un navegador de verdad —una
    /// sesión que sobrevive al alta, o un alta que se corta sola a media petición.
    ///
    /// <c>StartsWithSegments</c> y no una comparación de cadenas: cubre de una vez las DOS rutas de
    /// la pantalla —<c>/signup</c> y <c>/signup/{patrocinador}</c>, que en un evento es la que de
    /// verdad se usa— sin dejar pasar un <c>/signupdelotro</c> que empiece igual y no sea esto.
    /// </remarks>
    public static bool IsSignupNavigation(HttpContext context, string? signupPage)
    {
        // Un portal sin pantalla de alta —administración— no tiene nada que mirar aquí.
        if (string.IsNullOrWhiteSpace(signupPage)) return false;

        // Sin sesión no hay nada que matar, que es el caso de casi todo el mundo que abre el alta.
        if (context.User?.Identity?.IsAuthenticated != true) return false;

        // Solo el navegador CARGANDO la página. Ni el WebSocket del circuito, ni /_framework, ni el
        // CSS, ni los POST del propio asistente de alta: cortar cualquiera de esos dejaría el alta a
        // medias sin que el usuario supiera por qué.
        if (!HttpMethods.IsGet(context.Request.Method)) return false;
        if (!AcceptsHtml(context)) return false;

        return context.Request.Path.StartsWithSegments(
            signupPage, StringComparison.OrdinalIgnoreCase);
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

    private static bool YaLlevaElAviso(HttpRequest request) =>
        request.Query.ContainsKey(ClosedQueryParam);

    /// <summary>
    /// La MISMA dirección con la marca del aviso añadida.
    /// </summary>
    /// <remarks>
    /// La misma y no otra, y eso es media decisión: el patrocinador de la ruta con slug y lo que
    /// traiga la query —campañas, seguimiento— tienen que llegar enteros al asistente de alta. Se
    /// reconstruye con <c>ToUriComponent()</c>, que devuelve el camino tal y como viaja por el cable;
    /// usar <c>Value</c> re-emitiría sin codificar un slug con caracteres que no son de una URL.
    /// </remarks>
    private static string ConAviso(HttpRequest request)
    {
        var separador = request.QueryString.HasValue ? '&' : '?';

        return $"{request.PathBase.ToUriComponent()}{request.Path.ToUriComponent()}" +
               $"{request.QueryString.ToUriComponent()}" +
               $"{separador}{ClosedQueryParam}={ClosedQueryValue}";
    }
}
