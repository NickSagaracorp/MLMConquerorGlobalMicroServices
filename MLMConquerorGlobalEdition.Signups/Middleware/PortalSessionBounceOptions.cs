namespace MLMConquerorGlobalEdition.Signups.Middleware;

/// <summary>
/// Lo que hay que saber para mandar el navegador a cerrar sesión en los portales antes de pintar el
/// asistente de alta. Sale entero de configuración: los portales cambian de dirección de un entorno
/// a otro, y una lista escrita a fuego aquí sería una que nadie puede corregir sin recompilar.
/// </summary>
public sealed record PortalSessionBounceOptions
{
    /// <summary>
    /// El interruptor. Apagado, la aplicación de alta se comporta como si nada de esto existiera.
    /// </summary>
    /// <remarks>
    /// Existe para un despliegue en el que el alta viva en otro dominio registrable que los
    /// portales: allí el rebote no puede funcionar —la cookie del portal es <c>SameSite=Strict</c> y
    /// el navegador no la manda entre sitios distintos—, y dejarlo encendido solo añadiría dos saltos
    /// inútiles delante de cada alta.
    /// </remarks>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// El origen público de ESTA aplicación, con el que se construye el destino de vuelta. Vacío,
    /// se toma el de la petición.
    /// </summary>
    /// <remarks>
    /// Hace falta declararlo en cuanto hay un proxy delante: detrás de uno,
    /// <c>Request.Scheme</c> y <c>Request.Host</c> son los del salto interno, y el destino de vuelta
    /// construido con ellos apunta a una dirección que el navegador no puede alcanzar —y que además
    /// no estaría en la lista blanca del portal, así que el rebote acabaría en el login—.
    /// </remarks>
    public string? PublicBaseUrl { get; init; }

    /// <summary>
    /// Las rutas cuya CARGA dispara el rebote. Se comparan por segmentos, así que cada una cubre
    /// también su variante con el slug del patrocinador.
    /// </summary>
    /// <remarks>
    /// Las dos pantallas de alta, la de embajador y la de miembro. Por segmentos y no por igualdad
    /// porque la ruta que de verdad se usa en un evento es la del sitio replicado
    /// (<c>/ambassador-join/{patrocinador}</c>) y no la raíz; y por segmentos y no por prefijo de
    /// cadena para que un <c>/ambassador-joinotracosa</c> no entre por parecerse.
    /// </remarks>
    public string[] Paths { get; init; } = ["/ambassador-join", "/member-join"];

    /// <summary>
    /// Los portales por los que pasa el navegador, EN ORDEN, antes de volver aquí.
    /// </summary>
    /// <remarks>
    /// Es una lista y no un portal único a propósito. La regla del dueño no distingue portales
    /// —«debe morir cualquier sesión abierta en esa computadora»— y en un portátil de evento puede
    /// haber abierta tanto una sesión del centro de negocios como una de administración; la segunda
    /// es la más peligrosa de las dos. Con la lista, cubrir el segundo portal es una línea de
    /// configuración y no una rama de código.
    ///
    /// EL RECORRIDO TERMINA SIEMPRE: la marca del regreso es un contador que solo sube y cuyo techo
    /// es la longitud de esta lista.
    /// </remarks>
    public PortalStopOptions[] Portals { get; init; } = [];

    /// <summary>Cuánto se espera a que un portal conteste antes de darlo por caído.</summary>
    /// <remarks>
    /// Corto a propósito: esto corre DELANTE de cada carga del alta, así que es tiempo que el
    /// visitante mira una pantalla en blanco. Más vale saltarse el cierre de un portal lento que
    /// hacer esperar a una sala entera.
    /// </remarks>
    public int ProbeTimeoutMilliseconds { get; init; } = 1500;

    /// <summary>Cuánto vale la respuesta de un sondeo antes de volver a preguntar.</summary>
    /// <remarks>
    /// Sin esto habría un sondeo por cada carga de la pantalla, que en un evento son muchas. Con
    /// ventana, el estado de un portal se pregunta como mucho una vez cada tantos segundos.
    ///
    /// LA VENTANA CORTA EN LOS DOS SENTIDOS y por eso es corta: un portal que se cae DENTRO de una
    /// ventana en la que se le dio por vivo le cuesta a alguien una navegación fallida, y uno que
    /// revive dentro de una en la que se le dio por muerto sigue sin cerrarse hasta que expire.
    /// </remarks>
    public int ProbeCacheSeconds { get; init; } = 30;
}

/// <summary>Un portal del recorrido.</summary>
public sealed record PortalStopOptions
{
    /// <summary>Cómo se le llama en los registros. No lo ve ningún usuario.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// La dirección ABSOLUTA de su cierre de sesión, la misma a la que va su propio botón de salir.
    /// </summary>
    /// <remarks>
    /// Ese endpoint es quien ejecuta <c>PortalSignOut.KillAsync</c>: el refresh token en la API, la
    /// entrada del almacén de sesión, las tres cookies de reto, la cookie de sesión y el principal de
    /// la petición. El rebote no reimplementa nada de eso; lo INVOCA, que es la única forma de que
    /// las dos salidas no se desincronicen.
    /// </remarks>
    public string SignOutUrl { get; init; } = string.Empty;

    /// <summary>
    /// Una dirección suya barata con la que saber si está en pie. Vacía, se sondea
    /// <see cref="SignOutUrl"/>.
    /// </summary>
    public string? ProbeUrl { get; init; }
}
