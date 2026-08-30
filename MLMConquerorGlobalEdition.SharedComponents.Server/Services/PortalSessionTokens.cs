using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using MLMConquerorGlobalEdition.ClientCore;

namespace MLMConquerorGlobalEdition.SharedComponents.Server.Services;

/// <summary>
/// Los tokens VIGENTES de cada sesión del portal, y el único sitio desde el que se renuevan.
///
/// Tres piezas le preguntan: <see cref="SessionExpiryMiddleware"/> en cada navegación,
/// <see cref="ApiAuthHandler"/> en cada llamada a una API, y
/// <see cref="HttpContextAccessTokenProvider"/> cada vez que el gateway necesita un Bearer.
/// </summary>
/// <remarks>
/// POR QUÉ NO BASTA CON LA COOKIE, que es donde viven los tokens de verdad.
///
/// La cookie de sesión solo se puede reescribir sobre una respuesta HTTP que todavía no haya
/// empezado. Dentro de un circuito de Blazor Server no la hay: la respuesta a mano es la del
/// WebSocket y ya empezó (<c>Response.HasStarted = true</c>). Es el mismo muro contra el que se dio
/// el arreglo de la sesión caducada, y renovar choca con él igual — si la renovación ocurre dentro
/// del circuito, la cookie se queda con la pareja vieja.
///
/// Eso, por sí solo, no sería grave: bastaría con reemitirla en la siguiente navegación. Lo que lo
/// hace grave es que LA API ROTA EL REFRESH TOKEN. Si el circuito renueva y la cookie conserva el
/// refresh viejo, la siguiente navegación intentaría renovar con un token que la API ya invalidó, y
/// el usuario caería al login a mitad de sesión. Por eso este almacén NO es por circuito —que es lo
/// primero que se piensa— sino POR SESIÓN: el circuito y el middleware de la petición siguiente son
/// dos mundos distintos que tienen que estar mirando la misma pareja de tokens, y lo único que
/// comparten es la identidad de la sesión.
///
/// De ahí el claim <c>portal_session</c> que <see cref="AuthEndpoints"/> pone al firmar: un
/// identificador que no vale como credencial —no abre nada, solo nombra una entrada— y que viaja en
/// la misma cookie que el resto.
///
/// LA CARRERA, que es el otro motivo de que esto exista. Cuando el JWT caduca, lo normal no es que
/// UNA cosa lo descubra: una pantalla con tres grids lanza tres llamadas a la vez y las tres ven el
/// token caducado en el mismo instante. Con la rotación, tres renovaciones simultáneas con el mismo
/// refresh token significan una que funciona y dos que reciben INVALID_REFRESH_TOKEN, y esas dos
/// matan la sesión que la primera acababa de salvar. Aquí se resuelve con un semáforo POR SESIÓN y
/// una segunda comprobación dentro: el primero renueva, los demás esperan y se llevan lo que él
/// consiguió. Una sola llamada a la API por caducidad, siempre.
///
/// QUÉ PASA SI ESTE ALMACÉN PIERDE UNA ENTRADA —se reinicia el portal, o se poda por inactividad—:
/// se cae a los tokens de la cookie, que son los últimos que el middleware alcanzó a reemitir. Si la
/// última rotación fue dentro de un circuito y no llegó a haber navegación después, esa pareja está
/// gastada y la sesión acaba en el login con su aviso. Es el comportamiento correcto para una sesión
/// que no se puede renovar, y es la razón por la que el middleware reemite la cookie EN CUANTO
/// puede en vez de esperar a que caduque algo.
///
/// SINGLETON, a propósito: el circuito, el middleware y el manejador de las APIs viven en ámbitos de
/// DI distintos —ese fue el fallo que arregló 3763f9e— y tienen que ver el mismo diccionario.
/// </remarks>
public sealed class PortalSessionTokens
{
    /// <summary>
    /// Cuánto se guarda una sesión a la que nadie toca. Tiene que ser MAYOR que la cookie más larga
    /// de los dos portales (24 h en el centro de negocios, 8 h en administración): podar antes
    /// dejaría vivo un usuario cuya última rotación solo estaba aquí.
    /// </summary>
    /// <remarks>
    /// Las dos cookies son deslizantes, así que una sesión activa nunca envejece: mientras el
    /// usuario trabaje, su entrada se toca en cada llamada. Lo que se poda es lo que lleva más de un
    /// día entero sin dar señales, y eso ya no puede volver.
    /// </remarks>
    public static readonly TimeSpan DefaultIdleWindow = TimeSpan.FromHours(26);

    /// <summary>
    /// A partir de cuántas sesiones vivas se empieza a mirar si hay algo que podar. Por debajo de
    /// esto la memoria no es un problema y recorrer el diccionario solo gasta tiempo.
    /// </summary>
    private const int PruneThreshold = 512;

    /// <summary>Cada cuánto, como mucho, se recorre el diccionario buscando entradas muertas.</summary>
    private static readonly TimeSpan PruneInterval = TimeSpan.FromMinutes(5);

    private readonly AuthTokenRefresher              _refresher;
    private readonly ILogger<PortalSessionTokens>    _logger;
    private readonly TimeSpan                        _idleWindow;

    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);

    /// <summary>Cuándo se podó por última vez, en la cuenta monótona de la máquina.</summary>
    private long _lastPrune = Environment.TickCount64;

    public PortalSessionTokens(
        AuthTokenRefresher           refresher,
        ILogger<PortalSessionTokens> logger,
        TimeSpan?                    idleWindow = null)
    {
        _refresher  = refresher;
        _logger     = logger;
        _idleWindow = idleWindow ?? DefaultIdleWindow;
    }

    /// <summary>Cuántas sesiones tiene ahora mismo. Para las pruebas y para diagnosticar.</summary>
    public int Count => _sessions.Count;

    // ===============================================================================================
    //  Lo que se lee de la cookie
    // ===============================================================================================

    /// <summary>
    /// El identificador de sesión del portal, o null si esta cookie no lo lleva.
    /// </summary>
    /// <remarks>
    /// Una cookie sin él es una sesión firmada ANTES de que existiera la renovación. Sigue valiendo
    /// para entrar —el claim del token de acceso está donde siempre—, simplemente no se puede
    /// renovar, y cuando su JWT caduque acabará en el login como acababa antes.
    /// </remarks>
    public static string? SessionIdOf(ClaimsPrincipal? user) =>
        user?.FindFirst(SessionExpiry.SessionIdClaim)?.Value;

    /// <summary>
    /// La pareja de tokens tal y como viaja en la cookie, o null si no hay token de acceso.
    /// </summary>
    /// <remarks>
    /// El de refresco puede faltar —cookies antiguas, o una API que no lo entregara— y entonces sale
    /// cadena vacía: hay con qué llamar a las APIs hasta que caduque, pero no con qué renovar.
    /// </remarks>
    public static SessionTokens? FromClaims(ClaimsPrincipal? user)
    {
        var accessToken = user?.FindFirst(SessionExpiry.AccessTokenClaim)?.Value;
        if (string.IsNullOrEmpty(accessToken)) return null;

        var refreshToken = user?.FindFirst(SessionExpiry.RefreshTokenClaim)?.Value;
        return new SessionTokens(accessToken, refreshToken ?? string.Empty);
    }

    // ===============================================================================================
    //  Lo que sabe el almacén
    // ===============================================================================================

    /// <summary>
    /// Los tokens VIGENTES de este usuario: los del almacén si los hay —que son los más nuevos,
    /// porque aquí se escribe antes que en la cookie— y si no, los de su cookie.
    /// </summary>
    /// <remarks>
    /// No renueva nada. Es la lectura barata que hacen el middleware para decidir si hay algo que
    /// juzgar y el manejador para decidir si esta llamada lleva sesión o es anónima.
    /// </remarks>
    public SessionTokens? Current(ClaimsPrincipal? user)
    {
        var sessionId = SessionIdOf(user);

        if (sessionId is not null && _sessions.TryGetValue(sessionId, out var session))
        {
            session.Touch();
            return session.Tokens;
        }

        return FromClaims(user);
    }

    /// <summary>
    /// Deja los tokens recién firmados. Lo llama la puerta al completar el inicio de sesión.
    /// </summary>
    public void Seed(string sessionId, SessionTokens tokens)
    {
        if (string.IsNullOrEmpty(sessionId)) return;

        _sessions[sessionId] = new Session(tokens);
        PruneIfDue();
    }

    /// <summary>
    /// Olvida esta sesión. La llama la salida del portal, después de invalidar el refresh token en
    /// la API: dejar la entrada viva mantendría en memoria una credencial que ya no abre nada.
    /// </summary>
    public void Forget(ClaimsPrincipal? user) => Forget(SessionIdOf(user));

    /// <inheritdoc cref="Forget(ClaimsPrincipal?)"/>
    public void Forget(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId)) return;
        _sessions.TryRemove(sessionId, out _);
    }

    // ===============================================================================================
    //  La renovación
    // ===============================================================================================

    /// <summary>
    /// Devuelve los tokens vigentes de este usuario, renovándolos contra la API si el de acceso ya
    /// caducó. Null significa que esta sesión está muerta y hay que mandar al usuario al login.
    /// </summary>
    /// <remarks>
    /// Null también cuando el usuario es anónimo o su cookie no lleva token: quien llama distingue
    /// los dos casos preguntando antes por <see cref="Current"/>, que es null solo en el segundo.
    ///
    /// UN SOLO VUELO POR SESIÓN. El semáforo es por sesión y no global: dos usuarios distintos
    /// renuevan a la vez sin estorbarse, que es lo que hay que hacer con un portal de ciento
    /// diecinueve mil cuentas. Dentro del semáforo se vuelve a mirar el token, y esa segunda mirada
    /// es la que hace que de tres llamadas simultáneas salga UNA renovación: las otras dos entran
    /// cuando la primera ya guardó la pareja nueva, ven un token vivo y se lo llevan.
    /// </remarks>
    public async Task<SessionTokens?> EnsureFreshAsync(
        ClaimsPrincipal? user, CancellationToken ct = default)
    {
        var current = Current(user);
        if (current is null) return null;

        if (!SessionExpiry.IsExpired(current.AccessToken)) return current;

        // Caducado y sin con qué renovar: esta sesión no tiene salvación. Es el caso de las cookies
        // firmadas antes de que existiera la renovación, y también el de un token de refresco que ya
        // se gastó y se borró.
        if (string.IsNullOrEmpty(current.RefreshToken)) return null;

        var sessionId = SessionIdOf(user);

        // Sin identificador no hay entrada que sincronizar. Se renueva igual —es mejor que mandar al
        // login— pero sin un solo vuelo y sin poder guardar el resultado en ninguna parte.
        if (string.IsNullOrEmpty(sessionId))
            return await _refresher.RefreshAsync(current.RefreshToken, ct);

        var session = _sessions.GetOrAdd(sessionId, _ => new Session(current));

        await session.Gate.WaitAsync(ct);
        try
        {
            // LA SEGUNDA MIRADA. Si otra llamada de esta misma sesión renovó mientras se esperaba,
            // aquí ya hay una pareja buena: usarla es lo correcto, y lanzar otra renovación sería
            // gastar el refresh token que aquella acaba de recibir.
            var vigentes = session.Tokens;
            if (!SessionExpiry.IsExpired(vigentes.AccessToken))
                return vigentes;

            var renovados = await _refresher.RefreshAsync(vigentes.RefreshToken, ct);

            if (renovados is null)
            {
                // Refresco caducado, revocado o API caída. Fuera la entrada: conservarla solo
                // serviría para que la siguiente llamada volviera a intentarlo con lo mismo.
                _sessions.TryRemove(sessionId, out _);
                _logger.LogInformation(
                    "La sesión {Sesion} no se pudo renovar: se da por muerta.", sessionId);
                return null;
            }

            session.Tokens = renovados;
            session.Touch();

            _logger.LogDebug("Sesión {Sesion} renovada.", sessionId);
            return renovados;
        }
        finally
        {
            session.Gate.Release();
        }
    }

    // ===============================================================================================
    //  La poda
    // ===============================================================================================

    /// <summary>
    /// Quita las sesiones que llevan más de <see cref="_idleWindow"/> sin que nadie las toque.
    /// </summary>
    /// <remarks>
    /// Sin esto el diccionario solo crece: cada inicio de sesión deja una entrada y nada la quita
    /// salvo una salida explícita, que es justo lo que la mitad de los usuarios no hace nunca.
    ///
    /// Se hace de forma oportunista —al firmar una sesión nueva, y como mucho una vez cada cinco
    /// minutos— y no con un temporizador: un temporizador sería un servicio alojado más que arrancar
    /// y parar en los dos portales para ahorrar un recorrido de un diccionario que casi siempre está
    /// por debajo del umbral.
    /// </remarks>
    public void Prune()
    {
        var ahora = Environment.TickCount64;
        var limite = (long)_idleWindow.TotalMilliseconds;

        foreach (var (sessionId, session) in _sessions)
        {
            if (ahora - session.Touched > limite)
                _sessions.TryRemove(sessionId, out _);
        }
    }

    private void PruneIfDue()
    {
        if (_sessions.Count < PruneThreshold) return;

        var ahora    = Environment.TickCount64;
        var anterior = Interlocked.Read(ref _lastPrune);

        if (ahora - anterior < (long)PruneInterval.TotalMilliseconds) return;

        // Solo uno poda: quien consiga cambiar la marca. Los demás siguen su camino.
        if (Interlocked.CompareExchange(ref _lastPrune, ahora, anterior) != anterior) return;

        Prune();
    }

    /// <summary>
    /// Una sesión del portal: su pareja de tokens, el semáforo que serializa sus renovaciones y
    /// cuándo se la tocó por última vez.
    /// </summary>
    private sealed class Session
    {
        /// <summary>
        /// <c>volatile</c> porque se lee fuera del semáforo —desde <see cref="Current"/>— y se
        /// escribe dentro: sin él, un hilo podría seguir viendo la pareja vieja después de que otro
        /// la haya sustituido.
        /// </summary>
        private volatile SessionTokens _tokens;

        private long _touched = Environment.TickCount64;

        public Session(SessionTokens tokens) => _tokens = tokens;

        public SemaphoreSlim Gate { get; } = new(1, 1);

        public SessionTokens Tokens
        {
            get => _tokens;
            set => _tokens = value;
        }

        public long Touched => Interlocked.Read(ref _touched);

        public void Touch() => Interlocked.Exchange(ref _touched, Environment.TickCount64);
    }
}
