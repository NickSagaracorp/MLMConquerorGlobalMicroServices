using System.Globalization;

namespace MLMConquerorGlobalEdition.SharedKernel.Portal;

/// <summary>
/// EL REBOTE: cargar la aplicación de alta manda el navegador, UNA SOLA VEZ POR PORTAL, a la
/// dirección de cierre de sesión de cada portal, y vuelve.
///
/// Aquí vive la parte que las DOS PUNTAS del rebote tienen que entender igual: cómo se marca el
/// regreso para que no haya bucle, cómo se conserva entero lo que traía la dirección —el slug del
/// patrocinador el primero— y, sobre todo, QUÉ DESTINOS ACEPTA el cierre de sesión del portal.
/// </summary>
/// <remarks>
/// POR QUÉ EL CIERRE OCURRE ASÍ Y NO EN UN MIDDLEWARE DEL PORTAL. La regla del dueño es que cargar
/// el alta mate cualquier sesión abierta en ese ordenador. El alta de verdad no vive en el portal
/// —vive en su propia aplicación, en otro origen—, así que el portal ya no tiene ninguna ruta que
/// vigilar: cuando el navegador pide la pantalla de alta, esa petición no pasa por él. Lo único que
/// puede hacer que la cookie del portal desaparezca es una petición AL PORTAL desde ese mismo
/// navegador, y la única forma de provocarla desde otro origen es una navegación del navegador.
///
/// CUBRE TODAS LAS FORMAS DE LLEGAR AL ALTA —enlace, marcador, o el enlace del sitio replicado del
/// patrocinador, que en un evento es el caso normal— porque no depende de por dónde se entró, sino
/// de que la página se cargue.
///
/// EL LÍMITE QUE HAY QUE CONOCER ANTES DE DESPLEGAR: la cookie de sesión de los portales es
/// <c>SameSite=Strict</c>, y un navegador NO la manda en una navegación entre SITIOS distintos
/// —sitio es el dominio registrable, no el origen: el puerto no cuenta y el subdominio tampoco—. El
/// rebote mata la sesión mientras la aplicación de alta y los portales cuelguen del MISMO dominio
/// registrable (<c>alta.ejemplo.com</c> y <c>portal.ejemplo.com</c>, o dos puertos de
/// <c>localhost</c>). Si algún día el alta se sirve desde otro dominio, la petición de cierre
/// llegaría sin cookie: el portal no vería sesión, no mataría nada y el alta se abriría igual, sin
/// un solo error a la vista. Eso NO se arregla aquí —se arreglaría bajando la cookie del portal a
/// <c>Lax</c>, que es debilitar su defensa contra CSRF— y por eso queda escrito donde se lee antes
/// de mover un despliegue.
/// </remarks>
public static class PortalSessionBounce
{
    /// <summary>
    /// La marca del regreso: cuántos portales del recorrido ya se visitaron.
    /// </summary>
    /// <remarks>
    /// POR QUÉ UN CONTADOR Y NO UN SÍ/NO. El recorrido puede tener más de un portal —hoy el centro
    /// de negocios y administración—, y con un booleano el segundo no llegaría a visitarse nunca.
    /// Con el contador, cada vuelta sabe cuál es el siguiente y el recorrido TERMINA SIEMPRE: el
    /// número solo sube, y el máximo es la longitud de la lista.
    ///
    /// POR QUÉ VIAJA EN LA URL. La página se pinta en otra petición distinta de la que rebota
    /// —además, en un circuito—, así que <c>HttpContext.Items</c> no llega. Y una cookie para decir
    /// algo que se lee una vez es basura que hay que acordarse de borrar, además de no funcionar
    /// para quien las tenga bloqueadas, que es justo cuando el bucle sería infinito. La URL es lo
    /// único que las dos peticiones comparten.
    /// </remarks>
    public const string StepQueryParam = "portal_session";

    /// <summary>
    /// El nombre del parámetro con el que el cierre de sesión del portal recibe a dónde volver.
    /// </summary>
    public const string ReturnUrlQueryParam = "returnUrl";

    // ===============================================================================================
    //  La marca del regreso
    // ===============================================================================================

    /// <summary>
    /// Cuántos portales del recorrido dice esta dirección que ya se visitaron.
    /// </summary>
    /// <param name="stepCount">Cuántos portales tiene el recorrido; es el techo del contador.</param>
    /// <remarks>
    /// SE ACOTA, y no es una comprobación de cortesía. El valor viene de la URL, así que lo escribe
    /// quien quiera: sin acotarlo, un <c>?portal_session=99</c> pegado a un enlace saltaría el cierre
    /// entero. Fuera del rango se trata como si no hubiera marca, o sea, se rebota.
    ///
    /// LO QUE ESTO NO ES: una autorización. Un valor DENTRO del rango sí salta los portales que diga,
    /// y eso no se puede cerrar sin guardar estado en el servidor. No hace falta cerrarlo: esta
    /// medida protege de un DESCUIDO —la persona anterior que no se acordó de salir—, no de alguien
    /// que ya controla el enlace que la víctima pulsa; quien controla el enlace no necesita saltarse
    /// nada, manda a donde quiera directamente.
    /// </remarks>
    public static int CompletedSteps(string? queryString, int stepCount)
    {
        if (stepCount <= 0) return 0;

        var raw = ValueOf(queryString, StepQueryParam);
        if (raw is null) return 0;

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var step))
            return 0;

        return step < 0 || step > stepCount ? 0 : step;
    }

    /// <summary>
    /// La MISMA dirección con la marca del regreso puesta en el paso que se le diga.
    /// </summary>
    /// <remarks>
    /// LA MISMA Y NO OTRA, y esa es media decisión: el slug del patrocinador va en el camino y lo que
    /// traiga la query —campañas, seguimiento— tiene que llegar entero al asistente de alta. Perder
    /// el patrocinador aquí sería reproducir, por otro camino, el mismo fallo que este trabajo viene
    /// a cerrar.
    ///
    /// Los pares que ya había se copian TAL CUAL, sin descodificar y volver a codificar: un slug o un
    /// valor de campaña con caracteres que no son de una URL sobrevive a un viaje de ida y vuelta
    /// solo si nadie lo reescribe por el camino.
    ///
    /// La marca anterior se QUITA antes de poner la nueva. Si no, cada vuelta añadiría la suya y a la
    /// tercera habría tres <c>portal_session</c> en la misma dirección, con el primero —el más
    /// viejo— mandando en casi todos los lectores de query.
    /// </remarks>
    public static string WithStep(string url, int step)
    {
        var mark   = url.IndexOf('?');
        var path   = mark < 0 ? url : url[..mark];
        var query  = mark < 0 ? string.Empty : url[(mark + 1)..];

        var pairs = SplitPairs(query)
            .Where(pair => !IsStepPair(pair))
            .ToList();

        pairs.Add($"{StepQueryParam}={step.ToString(CultureInfo.InvariantCulture)}");

        return $"{path}?{string.Join('&', pairs)}";
    }

    /// <summary>
    /// La dirección de cierre de sesión del portal con el destino de vuelta colgado.
    /// </summary>
    public static string SignOutUrlWithReturn(string portalSignOutUrl, string returnUrl)
    {
        var separator = portalSignOutUrl.Contains('?') ? '&' : '?';

        return $"{portalSignOutUrl}{separator}{ReturnUrlQueryParam}=" +
               Uri.EscapeDataString(returnUrl);
    }

    // ===============================================================================================
    //  La lista blanca del destino de vuelta
    // ===============================================================================================

    /// <summary>
    /// ¿Puede el cierre de sesión del portal mandar el navegador a este destino?
    /// </summary>
    /// <remarks>
    /// ESTO ES OBLIGATORIO Y NO ES UNA COMPROBACIÓN DE FORMATO. Sin ella,
    /// <c>/account/logout?returnUrl=…</c> sería una redirección abierta: cualquiera podría publicar
    /// un enlace que EMPIEZA en el dominio del portal —el que el usuario reconoce, el que mira antes
    /// de pulsar— y termina donde el atacante quiera. Ese salto de confianza es el valor entero del
    /// truco: la misma pantalla de credenciales falsa, servida desde el dominio del atacante, no
    /// engaña a casi nadie; alcanzada desde un enlace del portal, engaña a mucha gente. Y aquí el
    /// salto ocurre justo después de cerrar la sesión, que es el momento en el que un usuario espera
    /// que le vuelvan a pedir la contraseña.
    ///
    /// SE FALLA CERRADO: sin lista, o con la lista vacía, no se acepta ningún destino y el portal se
    /// queda con su propia pantalla de login. Una lista que se quedó sin configurar tiene que
    /// romper el rebote —que es una comodidad—, nunca abrir la redirección —que es el agujero—.
    ///
    /// QUÉ SE COMPARA. Origen ENTERO (esquema, anfitrión y puerto) idéntico al de alguna entrada, y
    /// camino por debajo del suyo POR SEGMENTOS. Comparar por prefijo de cadena es el error clásico:
    /// con <c>https://alta.ejemplo.com</c> en la lista, <c>https://alta.ejemplo.com.malo.io</c>
    /// pasaría, y con <c>/alta</c> pasaría <c>/altaajena</c>. <see cref="Uri"/> además normaliza los
    /// <c>..</c> del camino, así que no hay forma de salirse del prefijo escalando directorios.
    ///
    /// LO QUE SE RECHAZA ANTES DE MIRAR LA LISTA:
    ///
    ///   • Lo que no es una dirección ABSOLUTA. De paso caen las protocolo-relativas
    ///     (<c>//malo.io/x</c>), que un navegador sí resuelve como otro sitio y
    ///     <see cref="Uri.TryCreate(string, UriKind, out Uri)"/> no da por absolutas.
    ///   • Lo que no es <c>http</c> ni <c>https</c>: <c>javascript:</c>, <c>data:</c> y compañía no
    ///     son sitios a los que volver.
    ///   • Lo que trae credenciales en la autoridad
    ///     (<c>https://alta.ejemplo.com@malo.io</c>): la comparación de anfitrión ya lo cazaría
    ///     —el anfitrión real es <c>malo.io</c>—, pero se corta antes porque la única razón de
    ///     escribir eso es que un humano lea mal la barra de direcciones.
    ///   • Lo que trae una barra invertida o un carácter de control. La primera la normalizan unos
    ///     analizadores y otros no, y esa discrepancia es de donde salen los saltos de autoridad; el
    ///     segundo es lo que se usa para partir una cabecera <c>Location</c> en dos.
    /// </remarks>
    public static bool IsAllowedReturnUrl(string? returnUrl, IReadOnlyCollection<string>? allowList)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))    return false;
        if (allowList is null || allowList.Count == 0) return false;

        if (returnUrl.Contains('\\'))                            return false;
        if (returnUrl.Any(c => char.IsControl(c)))               return false;

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var target)) return false;

        if (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps) return false;
        if (!string.IsNullOrEmpty(target.UserInfo))                                    return false;

        foreach (var entry in allowList)
        {
            if (string.IsNullOrWhiteSpace(entry)) continue;
            if (!Uri.TryCreate(entry, UriKind.Absolute, out var allowed)) continue;

            if (!string.Equals(target.Scheme, allowed.Scheme, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(target.Host, allowed.Host, StringComparison.OrdinalIgnoreCase))
                continue;
            if (target.Port != allowed.Port)
                continue;
            if (!PathIsUnder(target.AbsolutePath, allowed.AbsolutePath))
                continue;

            return true;
        }

        return false;
    }

    // ===============================================================================================
    //  Lo de dentro
    // ===============================================================================================

    /// <summary>Camino por debajo del prefijo, contando SEGMENTOS y no letras.</summary>
    private static bool PathIsUnder(string path, string prefix)
    {
        var trimmed = prefix.TrimEnd('/');

        // La raíz admite cualquier camino del mismo origen.
        if (trimmed.Length == 0) return true;

        if (!path.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase)) return false;

        return path.Length == trimmed.Length || path[trimmed.Length] == '/';
    }

    /// <summary>El primer valor de un parámetro, o null si la query no lo trae.</summary>
    private static string? ValueOf(string? queryString, string name)
    {
        foreach (var pair in SplitPairs(Trimmed(queryString)))
        {
            var equals = pair.IndexOf('=');
            var key    = equals < 0 ? pair : pair[..equals];

            if (!string.Equals(Unescape(key), name, StringComparison.OrdinalIgnoreCase)) continue;

            return equals < 0 ? string.Empty : Unescape(pair[(equals + 1)..]);
        }

        return null;
    }

    private static bool IsStepPair(string pair)
    {
        var equals = pair.IndexOf('=');
        var key    = equals < 0 ? pair : pair[..equals];

        return string.Equals(Unescape(key), StepQueryParam, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SplitPairs(string query) =>
        Trimmed(query).Split('&', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>La query sin la interrogación de delante, la traiga o no.</summary>
    private static string Trimmed(string? query) =>
        string.IsNullOrEmpty(query) ? string.Empty
        : query[0] == '?'           ? query[1..]
        :                             query;

    /// <summary>
    /// Descodifica solo para COMPARAR nombres. Los pares que se conservan nunca pasan por aquí: se
    /// copian tal cual, que es lo único que garantiza que vuelvan como salieron.
    /// </summary>
    private static string Unescape(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        catch (UriFormatException)
        {
            // Un porcentaje suelto no es un nombre que nos interese; devolverlo crudo basta para
            // que la comparación falle y el par se conserve.
            return value;
        }
    }
}
