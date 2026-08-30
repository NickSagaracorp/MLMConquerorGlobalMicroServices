using MLMConquerorGlobalEdition.SharedKernel.Portal;

namespace MLMConquerorGlobalEdition.Signups.Middleware;

/// <summary>
/// CARGAR EL ASISTENTE DE ALTA MATA CUALQUIER SESIÓN ABIERTA EN ESE NAVEGADOR.
/// </summary>
/// <remarks>
/// EL ESCENARIO, que es real y ocurre en cada evento: en un mismo ordenador se dan de alta varias
/// personas seguidas. La persona A termina y se levanta sin salir; la persona B se sienta y abre el
/// alta —casi siempre por el enlace del sitio replicado de su patrocinador—. Si la sesión de A sigue
/// viva en ese navegador, a B le basta con escribir cualquier dirección del portal para estar DENTRO
/// de la cuenta de A: su genealogía, sus comisiones, sus datos personales. Nadie tiene que hacer
/// nada malo para que pase; basta con que A no se acuerde de salir.
///
/// La regla, en palabras del dueño: <em>«nunca desde una sesión iniciada. De hecho si carga la página
/// de signup debe morir cualquier sesión abierta en esa computadora.»</em>
///
/// POR QUÉ EL CORTE ESTÁ AQUÍ Y NO EN EL PORTAL. Hubo una versión anterior que era un middleware DEL
/// PORTAL vigilando su ruta <c>/signup</c>. Esa ruta era una copia atrasada del asistente —mandaba
/// un campo que el contrato de la API no tiene, así que las altas se guardaban sin patrocinador— y se
/// ha borrado. El alta solo vive aquí, que es OTRO ORIGEN: cuando el navegador pide esta pantalla, la
/// petición no pasa por ningún portal, y por tanto ningún portal puede enterarse ni borrar su propia
/// cookie. Lo único que puede provocar que esa cookie desaparezca es una petición al portal desde
/// este mismo navegador, y desde otro origen eso solo se consigue con una NAVEGACIÓN.
///
/// POR QUÉ UN MIDDLEWARE Y NO EL <c>OnInitializedAsync</c> DE LA PÁGINA. Dos razones, y la segunda es
/// la que cuenta. La primera, que la página se pinta en un circuito y allí la respuesta HTTP que hay
/// a mano ya empezó. La segunda: el rebote tiene que ocurrir ANTES de que el visitante empiece a
/// teclear. Desde la página, el navegador se iría a mitad del formulario y volvería con todo en
/// blanco.
///
/// QUÉ NO SE TOCA: lo que no es el navegador CARGANDO una pantalla de alta. Ni el WebSocket del
/// circuito, ni <c>/_framework</c>, ni el CSS, ni las llamadas del propio asistente a la API. Y las
/// rutas se comparan POR SEGMENTOS, así que <c>/ambassador-join</c> y
/// <c>/ambassador-join/{patrocinador}</c> entran y <c>/ambassador-joinotracosa</c> no.
/// </remarks>
public sealed class PortalSessionBounceMiddleware
{
    private readonly RequestDelegate                          _next;
    private readonly PortalSessionBounceOptions               _options;
    private readonly PortalReachability                       _reachability;
    private readonly ILogger<PortalSessionBounceMiddleware>   _logger;

    public PortalSessionBounceMiddleware(
        RequestDelegate                        next,
        PortalSessionBounceOptions             options,
        PortalReachability                     reachability,
        ILogger<PortalSessionBounceMiddleware> logger)
    {
        _next         = next;
        _options      = options;
        _reachability = reachability;
        _logger       = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsSignupNavigation(context, _options) && !context.Response.HasStarted)
        {
            var destino = await NextStopAsync(context);

            if (destino is not null)
            {
                context.Response.Redirect(destino);
                return;
            }
        }

        await _next(context);
    }

    /// <summary>
    /// ¿Esta petición es alguien CARGANDO una pantalla de alta en un navegador?
    /// </summary>
    /// <remarks>
    /// Público y estático para poder ejercer cada condición por separado desde las pruebas: las
    /// cuatro, mal puestas, producen fallos que no se ven leyendo el diff —una sesión que sobrevive
    /// al alta, o un alta que se corta sola a media petición y pierde lo tecleado—.
    /// </remarks>
    public static bool IsSignupNavigation(HttpContext context, PortalSessionBounceOptions options)
    {
        if (!options.Enabled)              return false;
        if (options.Portals.Length == 0)   return false;

        // Solo el navegador CARGANDO la página. Ni el WebSocket del circuito, ni /_framework, ni el
        // CSS, ni los POST del propio asistente: cortar cualquiera de esos dejaría el alta a medias
        // sin que el visitante supiera por qué.
        if (!HttpMethods.IsGet(context.Request.Method)) return false;
        if (!AcceptsHtml(context))                      return false;

        foreach (var path in options.Paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            if (context.Request.Path.StartsWithSegments(path, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// El siguiente portal al que mandar el navegador, o null si ya no queda ninguno al que ir.
    /// </summary>
    /// <remarks>
    /// SE EMPIEZA DONDE DIGA LA MARCA, que es lo que hace que esto no sea un bucle: la marca solo
    /// sube y su techo es la longitud de la lista, así que el recorrido termina siempre y como mucho
    /// hay un salto por portal. Una marca fuera de rango vale como si no hubiera ninguna, así que
    /// pegarle un número grande a la URL no salta el cierre.
    ///
    /// UN PORTAL QUE NO CONTESTA SE SALTA, y se sigue con el siguiente en vez de abandonar el
    /// recorrido: que administración esté caída no es razón para dejar viva una sesión del centro de
    /// negocios. Si no contesta ninguno, se devuelve null y el alta se abre igual, que es la regla
    /// que manda: el alta nunca se queda bloqueada.
    /// </remarks>
    private async Task<string?> NextStopAsync(HttpContext context)
    {
        var portals = _options.Portals;
        var done    = PortalSessionBounce.CompletedSteps(
            context.Request.QueryString.Value, portals.Length);

        for (var i = done; i < portals.Length; i++)
        {
            var portal = portals[i];

            if (string.IsNullOrWhiteSpace(portal.SignOutUrl)) continue;
            if (!await _reachability.IsUpAsync(portal, context.RequestAborted)) continue;

            // El destino de vuelta es ESTA MISMA dirección con la marca en el paso siguiente: el
            // slug del patrocinador y la query que trajera vuelven enteros. Perderlos aquí sería
            // reproducir por otro camino el mismo fallo que este trabajo viene a cerrar.
            var returnUrl = PortalSessionBounce.WithStep(CurrentUrl(context), i + 1);

            _logger.LogInformation(
                "Alta abierta en {Path}: el navegador pasa por el cierre de sesión de {Portal} " +
                "antes de pintar nada.",
                context.Request.Path.Value,
                string.IsNullOrWhiteSpace(portal.Name) ? portal.SignOutUrl : portal.Name);

            return PortalSessionBounce.SignOutUrlWithReturn(portal.SignOutUrl, returnUrl);
        }

        return null;
    }

    /// <summary>
    /// La dirección ABSOLUTA de esta petición, que es la que el portal necesita para poder devolver
    /// el navegador desde su origen.
    /// </summary>
    /// <remarks>
    /// <c>ToUriComponent()</c> y no <c>Value</c>: devuelve el camino tal y como viaja por el cable, y
    /// eso es lo que impide que un slug con caracteres que no son de una URL se reemita sin
    /// codificar.
    /// </remarks>
    private string CurrentUrl(HttpContext context)
    {
        var origin = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? $"{context.Request.Scheme}://{context.Request.Host.ToUriComponent()}"
            : _options.PublicBaseUrl!.TrimEnd('/');

        return origin
             + context.Request.PathBase.ToUriComponent()
             + context.Request.Path.ToUriComponent()
             + context.Request.QueryString.ToUriComponent();
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
}
