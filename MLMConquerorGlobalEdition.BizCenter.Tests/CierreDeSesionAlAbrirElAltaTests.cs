using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.ClientCore;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;
using MLMConquerorGlobalEdition.SharedKernel.Portal;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// CARGAR EL ASISTENTE DE ALTA TIENE QUE MATAR CUALQUIER SESIÓN ABIERTA EN ESE NAVEGADOR. Esta es
/// LA MITAD DEL PORTAL: lo que ocurre cuando el rebote de la aplicación de alta llega a su salida.
///
/// EL ESCENARIO, que es el que hay que tener en la cabeza leyendo este archivo: en un evento se dan
/// de alta varias personas seguidas en el mismo ordenador. La persona A termina y se levanta sin
/// salir; la persona B se sienta y abre el alta —normalmente por el enlace del sitio replicado de su
/// patrocinador—. Con la sesión de A viva, a B le basta con teclear cualquier dirección del portal
/// para estar dentro de la cuenta de A.
///
/// POR QUÉ ESTAS PRUEBAS CAMBIARON DE FORMA. Antes vigilaban un middleware DEL PORTAL sobre su ruta
/// <c>/signup</c>. Esa ruta era una copia atrasada del asistente —mandaba <c>SponsorMemberId</c>, un
/// campo que <c>AmbassadorSignupRequest</c> no tiene, así que las altas se guardaban sin
/// patrocinador— y se ha borrado. El alta solo vive en su propia aplicación, que es otro origen: el
/// portal ya no ve pasar esa navegación y no tiene nada que vigilar.
///
/// Ahora el corte lo da la aplicación de alta, que al cargarse manda el navegador UNA SOLA VEZ POR
/// PORTAL a <c>/account/logout</c> y vuelve. Lo que se prueba aquí es la mitad que sigue siendo del
/// portal, y sigue siendo lo mismo de antes más una cosa nueva:
///
///   • QUE MUERAN LAS CINCO COSAS, y en su orden. No cambió nada: sigue siendo
///     <see cref="PortalSignOut.KillAsync"/>, que es lo que se conservó entero del intento anterior.
///   • QUE EL DESTINO DE VUELTA ESTÉ EN UNA LISTA BLANCA. Esto es nuevo y es obligatorio: sin ello,
///     la salida del portal sería una redirección abierta.
///
/// La otra mitad —que el rebote ocurra una sola vez, que conserve el patrocinador y que el alta se
/// abra igual con el portal caído— vive donde vive el middleware que la hace, en
/// <c>Signups.Tests/RebotePortalSessionBounceTests</c>.
/// </summary>
public class CierreDeSesionAlAbrirElAltaTests
{
    private const string Login = "/login";

    /// <summary>El origen desde el que rebota la aplicación de alta.</summary>
    private const string Alta = "https://alta.ejemplo.com";

    private static readonly ChallengeCookieNames Cookies = new()
    {
        Login      = "mlm_pruebas_2fa_challenge",
        Enrollment = "mlm_pruebas_2fa_enrollment",
        Phone      = "mlm_pruebas_phone_challenge"
    };

    /// <summary>El centro de negocios, con la aplicación de alta admitida como destino de vuelta.</summary>
    private static readonly AuthPortalOptions CentroDeNegocios = new()
    {
        LoginPage                 = Login,
        TwoFactorPage             = "/two-factor",
        EnrollAuthenticatorPage   = "/enroll-authenticator",
        HomePage                  = "/",
        SignOutReturnUrlAllowList = [$"{Alta}/"]
    };

    /// <summary>Un portal que nunca configuró la lista. No acepta ningún destino.</summary>
    private static readonly AuthPortalOptions SinListaBlanca = CentroDeNegocios with
    {
        SignOutReturnUrlAllowList = null
    };

    // ===============================================================================================
    //  1. Las cinco cosas que tienen que morir cuando llega el rebote
    // ===============================================================================================

    /// <summary>
    /// LA PRUEBA DEL ESCENARIO. El rebote llega con la sesión de la persona anterior viva: la cookie
    /// de sesión se limpia y el navegador se va de vuelta al alta.
    /// </summary>
    [Fact]
    public async Task ElRebote_ConLaSesionDeOtroViva_LimpiaLaCookieDeSesion()
    {
        var mundo = new MundoDePruebas(TokenVivo());

        await mundo.SalirAsync(returnUrl: $"{Alta}/ambassador-join?portal_session=1");

        mundo.Autenticacion.Salidas.Should().Be(1,
            "la cookie de sesión de la persona anterior es lo primero que tiene que desaparecer");
    }

    /// <summary>
    /// Las TRES cookies de reto, no dos. Un segundo factor a medias o un alta de teléfono a medias
    /// de la persona anterior son credenciales de un solo paso que la siguiente podría canjear.
    /// </summary>
    [Fact]
    public async Task ElRebote_MataLasTresCookiesDeReto()
    {
        var mundo = new MundoDePruebas(TokenVivo());

        await mundo.SalirAsync(returnUrl: $"{Alta}/ambassador-join?portal_session=1");

        var escritas = mundo.CookiesEscritas();

        escritas.Should().Contain(c => c.StartsWith($"{Cookies.Login}="));
        escritas.Should().Contain(c => c.StartsWith($"{Cookies.Enrollment}="));
        escritas.Should().Contain(c => c.StartsWith($"{Cookies.Phone}="),
            "el reto del teléfono es el que se olvida, y sobrevive perfectamente a su dueño");
    }

    /// <summary>
    /// LA PIEZA QUE NO ESTÁ EN EL NAVEGADOR, y por eso es la que se olvida. El refresh token vive en
    /// la base de datos de la API, dura treinta días y sirve para pedir tokens de acceso nuevos sin
    /// contraseña: borrar la cookie sin invalidarlo no es cerrar la sesión, es esconderla.
    /// </summary>
    [Fact]
    public async Task ElRebote_InvalidaElRefreshTokenEnLaApi_ConElTokenDelUsuarioPuesto()
    {
        var token = TokenVivo();
        var mundo = new MundoDePruebas(token);

        await mundo.SalirAsync(returnUrl: $"{Alta}/ambassador-join?portal_session=1");

        mundo.Api.Peticiones.Should().ContainSingle()
            .Which.Should().Be(("POST", "/api/v1/auth/logout", $"Bearer {token}"),
                "la llamada va autenticada, así que tiene que salir ANTES de dejar anónimo el " +
                "principal de esta petición: invertir ese orden deja el refresco vivo para siempre " +
                "y sin un solo error a la vista");
    }

    /// <summary>
    /// La entrada del almacén en memoria del portal. Dejarla viva mantiene a mano una pareja de
    /// tokens con la que una petición en vuelo puede resucitar la sesión recién cerrada.
    /// </summary>
    [Fact]
    public async Task ElRebote_OlvidaLaEntradaDelAlmacenDeSesion()
    {
        var mundo = new MundoDePruebas(TokenVivo());
        mundo.Almacen.Count.Should().Be(1, "la sesión de la persona anterior estaba sembrada");

        await mundo.SalirAsync(returnUrl: $"{Alta}/ambassador-join?portal_session=1");

        mundo.Almacen.Count.Should().Be(0);
    }

    /// <summary>
    /// Y EL USUARIO DE ESTA PETICIÓN. <c>SignOutAsync</c> escribe una cabecera para el navegador; no
    /// toca lo que el resto de la tubería ya tiene en la mano.
    /// </summary>
    [Fact]
    public async Task ElRebote_DejaAnonimoAlUsuarioDeEstaPeticion()
    {
        var mundo = new MundoDePruebas(TokenVivo());

        await mundo.SalirAsync(returnUrl: $"{Alta}/ambassador-join?portal_session=1");

        mundo.Contexto.User.Identity?.IsAuthenticated.Should().NotBe(true);
    }

    /// <summary>
    /// Una sesión SIN el claim del token —firmada antes de que existiera la renovación, o por otra
    /// versión del portal— también tiene que morir. Aquí no se juzga una sesión, se garantiza que no
    /// quede ninguna.
    /// </summary>
    [Fact]
    public async Task SinElClaimDelToken_LaSesionMuereIgual()
    {
        var mundo = new MundoDePruebas(token: null);

        await mundo.SalirAsync(returnUrl: $"{Alta}/ambassador-join?portal_session=1");

        mundo.Autenticacion.Salidas.Should().Be(1);
        mundo.Api.Peticiones.Should().BeEmpty(
            "sin token no hay nada que invalidar al otro lado, pero la sesión local se cierra igual");
    }

    /// <summary>
    /// Quien llega al rebote SIN sesión —que es casi todo el mundo— no rompe nada: se le devuelve al
    /// alta igual y sin haber firmado ni cerrado nada.
    /// </summary>
    [Fact]
    public async Task SinSesion_ElReboteDevuelveAlAltaIgual()
    {
        var mundo = new MundoDePruebas(token: null, conSesion: false);

        var destino = await mundo.SalirAsync(returnUrl: $"{Alta}/ambassador-join?portal_session=1");

        destino.Should().Be($"{Alta}/ambassador-join?portal_session=1");
        mundo.Api.Peticiones.Should().BeEmpty();
    }

    // ===============================================================================================
    //  2. Las dos salidas de siempre, que no cambian
    // ===============================================================================================

    /// <summary>La salida normal del portal, sin destino de vuelta: al login de siempre.</summary>
    [Fact]
    public async Task LaSalidaDeLaPuerta_SigueLimpiandoTodoYYendoAlLogin()
    {
        var mundo = new MundoDePruebas(TokenVivo());

        var destino = await mundo.SalirAsync();

        destino.Should().Be(Login);
        mundo.Autenticacion.Salidas.Should().Be(1);

        var escritas = mundo.CookiesEscritas();
        escritas.Should().Contain(c => c.StartsWith($"{Cookies.Login}="));
        escritas.Should().Contain(c => c.StartsWith($"{Cookies.Enrollment}="));
        escritas.Should().Contain(c => c.StartsWith($"{Cookies.Phone}="));

        mundo.Api.Peticiones.Should().ContainSingle()
            .Which.Path.Should().Be("/api/v1/auth/logout");
    }

    /// <summary>
    /// Y la salida a la que manda el circuito cuando descubre su sesión caducada sigue llevando su
    /// aviso hasta el login. Es el arreglo de la sesión caducada, y este trabajo no lo toca.
    /// </summary>
    [Fact]
    public async Task LaSalidaDeUnaSesionCaducada_SigueLlevandoSuAvisoAlLogin()
    {
        var mundo = new MundoDePruebas(TokenVivo());

        var destino = await mundo.SalirAsync(reason: "session_expired");

        destino.Should().Be($"{Login}?error=session_expired");
    }

    // ===============================================================================================
    //  3. LA LISTA BLANCA DEL DESTINO DE VUELTA
    //
    //  Sin esto, /account/logout?returnUrl=… es una redirección abierta: un enlace que EMPIEZA en el
    //  dominio del portal —el que el usuario reconoce y mira antes de pulsar— y termina donde quiera
    //  el atacante, justo en el instante en que se le acaba de cerrar la sesión y espera que le
    //  vuelvan a pedir la contraseña. Ese salto de confianza es el valor entero del truco.
    // ===============================================================================================

    /// <summary>El camino bueno: un destino de la lista se sigue.</summary>
    [Fact]
    public async Task ConUnDestinoDeLaLista_ElNavegadorVuelveAlAlta()
    {
        var mundo = new MundoDePruebas(TokenVivo());

        var destino = await mundo.SalirAsync(returnUrl: $"{Alta}/ambassador-join?portal_session=1");

        destino.Should().Be($"{Alta}/ambassador-join?portal_session=1");
    }

    /// <summary>
    /// EL PATROCINADOR VUELVE ENTERO. Es la ruta que de verdad se usa en un evento —el enlace del
    /// sitio replicado— y perderlo aquí sería reproducir, por otro camino, el mismo fallo que este
    /// trabajo viene a cerrar: un alta guardada sin patrocinador.
    /// </summary>
    [Fact]
    public async Task ElDestinoDeVuelta_ConservaElPatrocinadorYLaQueryQueTraia()
    {
        var mundo   = new MundoDePruebas(TokenVivo());
        var volviendoA = $"{Alta}/ambassador-join/AMB-320189?utm_source=evento&portal_session=1";

        var destino = await mundo.SalirAsync(returnUrl: volviendoA);

        destino.Should().Be(volviendoA);
    }

    /// <summary>
    /// UN DESTINO EXTERNO SE RECHAZA. Es la prueba que justifica la lista entera: sin ella, este
    /// mismo enlace mandaría al usuario al sitio del atacante desde el dominio del portal.
    /// </summary>
    [Fact]
    public async Task UnDestinoEXTERNO_SeRechazaYElUsuarioAcabaEnElLoginDelPortal()
    {
        var mundo = new MundoDePruebas(TokenVivo());

        var destino = await mundo.SalirAsync(returnUrl: "https://malo.io/inicia-sesion");

        destino.Should().Be(Login, "un destino que no está en la lista no se sigue jamás");
        destino.Should().NotContain("malo.io");
    }

    /// <summary>
    /// Y SE RECHAZA SIN DEJAR VIVA LA SESIÓN. Importa el orden: primero se mata, después se decide a
    /// dónde. Al revés, un returnUrl malo sería además una forma de que la sesión sobreviviera.
    /// </summary>
    [Fact]
    public async Task UnDestinoRechazado_MataLaSesionIgual()
    {
        var mundo = new MundoDePruebas(TokenVivo());

        await mundo.SalirAsync(returnUrl: "https://malo.io/inicia-sesion");

        mundo.Autenticacion.Salidas.Should().Be(1);
        mundo.Almacen.Count.Should().Be(0);
        mundo.Api.Peticiones.Should().ContainSingle();
    }

    /// <summary>
    /// FALLA CERRADO. Un portal que nunca configuró la lista no acepta ningún destino. Una lista sin
    /// configurar tiene que romper el rebote —que es una comodidad— y nunca abrir la redirección.
    /// </summary>
    [Fact]
    public async Task SinListaConfigurada_NoSeAceptaNiSiquieraElDestinoBueno()
    {
        var mundo = new MundoDePruebas(TokenVivo());

        var destino = await mundo.SalirAsync(
            returnUrl: $"{Alta}/ambassador-join", portal: SinListaBlanca);

        destino.Should().Be(Login);
    }

    /// <summary>
    /// LAS FORMAS CON LAS QUE SE INTENTA COLAR UN DESTINO AJENO. Cada línea es una de las maneras
    /// conocidas de que una comprobación floja diga que sí.
    /// </summary>
    [Theory]
    // Otro sitio, sin más.
    [InlineData("https://malo.io/x")]
    // El clásico del prefijo de cadena: empieza igual y es otro dominio.
    [InlineData("https://alta.ejemplo.com.malo.io/x")]
    // Y el mismo por el otro lado.
    [InlineData("https://malo-alta.ejemplo.com.io/x")]
    // Credenciales en la autoridad, para que un humano lea mal la barra de direcciones.
    [InlineData("https://alta.ejemplo.com@malo.io/x")]
    // Otro esquema: no es un sitio al que volver.
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<h1>hola</h1>")]
    // Protocolo-relativa: el navegador la resuelve como otro sitio.
    [InlineData("//malo.io/x")]
    // Relativa: no es un destino absoluto y no puede validarse contra un origen.
    [InlineData("/ambassador-join")]
    // La barra invertida, que unos analizadores normalizan y otros no.
    [InlineData("https://alta.ejemplo.com\\@malo.io/x")]
    [InlineData("/\\malo.io/x")]
    // Partir la cabecera Location en dos.
    [InlineData("https://alta.ejemplo.com/x\r\nLocation: https://malo.io")]
    // Vacíos.
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DestinosQueNoSeAceptanNunca(string? returnUrl)
    {
        PortalSessionBounce
            .IsAllowedReturnUrl(returnUrl, CentroDeNegocios.SignOutReturnUrlAllowList)
            .Should().BeFalse();
    }

    /// <summary>El mismo anfitrión no basta: el origen es esquema, anfitrión y PUERTO.</summary>
    [Theory]
    [InlineData("http://alta.ejemplo.com/x",       "el esquema no es el mismo")]
    [InlineData("https://alta.ejemplo.com:8443/x", "el puerto no es el mismo")]
    public void ElOrigenSeComparaENTERO(string returnUrl, string porque)
    {
        PortalSessionBounce
            .IsAllowedReturnUrl(returnUrl, CentroDeNegocios.SignOutReturnUrlAllowList)
            .Should().BeFalse(porque);
    }

    /// <summary>Lo que sí se acepta, y por qué cada uno.</summary>
    [Theory]
    [InlineData("https://alta.ejemplo.com/")]
    [InlineData("https://alta.ejemplo.com/ambassador-join")]
    [InlineData("https://alta.ejemplo.com/ambassador-join/AMB-320189")]
    [InlineData("https://alta.ejemplo.com/ambassador-join/AMB-320189?portal_session=1")]
    // El esquema y el anfitrión no distinguen mayúsculas, y un navegador los manda como le apetece.
    [InlineData("HTTPS://ALTA.EJEMPLO.COM/ambassador-join")]
    public void DestinosQueSiSeAceptan(string returnUrl)
    {
        PortalSessionBounce
            .IsAllowedReturnUrl(returnUrl, CentroDeNegocios.SignOutReturnUrlAllowList)
            .Should().BeTrue();
    }

    /// <summary>
    /// Cuando la entrada de la lista lleva CAMINO, se compara por segmentos: un camino que empieza
    /// con las mismas letras y sigue con otras no está por debajo de él.
    /// </summary>
    [Theory]
    [InlineData("https://alta.ejemplo.com/alta",            true)]
    [InlineData("https://alta.ejemplo.com/alta/AMB-320189", true)]
    [InlineData("https://alta.ejemplo.com/altaajena",       false)]
    [InlineData("https://alta.ejemplo.com/otra-cosa",       false)]
    // Uri normaliza el escalado de directorios antes de que nadie lo compare.
    [InlineData("https://alta.ejemplo.com/alta/../otra",    false)]
    public void ConCaminoEnLaLista_SeComparaPorSegmentos(string returnUrl, bool admitido)
    {
        PortalSessionBounce
            .IsAllowedReturnUrl(returnUrl, [$"{Alta}/alta"])
            .Should().Be(admitido);
    }

    // ===============================================================================================
    //  4. La marca del regreso: una vez por portal y nunca un bucle
    // ===============================================================================================

    /// <summary>Sin marca, no se ha visitado ningún portal todavía.</summary>
    [Fact]
    public void SinMarca_NoSeHaVisitadoNingunPortal() =>
        PortalSessionBounce.CompletedSteps("?utm_source=evento", stepCount: 2).Should().Be(0);

    /// <summary>Con marca, se sigue por donde iba.</summary>
    [Theory]
    [InlineData("?portal_session=1", 1)]
    [InlineData("?portal_session=2", 2)]
    [InlineData("?a=b&portal_session=1&c=d", 1)]
    public void ConMarca_SeContinuaElRecorrido(string query, int esperado) =>
        PortalSessionBounce.CompletedSteps(query, stepCount: 2).Should().Be(esperado);

    /// <summary>
    /// UNA MARCA FUERA DE RANGO VALE COMO SI NO HUBIERA NINGUNA. Sin acotarla, pegar
    /// <c>?portal_session=99</c> a un enlace saltaría el cierre entero.
    /// </summary>
    [Theory]
    [InlineData("?portal_session=99")]
    [InlineData("?portal_session=-1")]
    [InlineData("?portal_session=lo-que-sea")]
    [InlineData("?portal_session=")]
    public void UnaMarcaFueraDeRango_ValeComoSiNoHubieraNinguna(string query) =>
        PortalSessionBounce.CompletedSteps(query, stepCount: 2).Should().Be(0);

    /// <summary>
    /// EL RECORRIDO TERMINA SIEMPRE. La marca solo sube y su techo es el número de portales: por eso
    /// no hay bucle posible, pase lo que pase con las cookies al otro lado.
    /// </summary>
    [Fact]
    public void ElRecorridoTermina_LaMarcaLlegaAlTecheYNoPasaDeAhi()
    {
        var url = "https://alta.ejemplo.com/ambassador-join/AMB-320189";

        for (var paso = 0; paso < 2; paso++)
        {
            PortalSessionBounce.CompletedSteps(SoloLaQuery(url), stepCount: 2).Should().Be(paso);
            url = PortalSessionBounce.WithStep(url, paso + 1);
        }

        PortalSessionBounce.CompletedSteps(SoloLaQuery(url), stepCount: 2).Should().Be(2,
            "y al llegar aquí ya no queda ningún portal al que ir");
    }

    /// <summary>
    /// LA MARCA SE PONE CONSERVANDO LO QUE YA TRAÍA LA DIRECCIÓN: el slug del patrocinador en el
    /// camino y la query de la campaña por la que llegó.
    /// </summary>
    [Fact]
    public void LaMarca_ConservaElPatrocinadorYLaQuery() =>
        PortalSessionBounce
            .WithStep("https://alta.ejemplo.com/ambassador-join/AMB-320189?utm_source=evento", 1)
            .Should().Be(
                "https://alta.ejemplo.com/ambassador-join/AMB-320189?utm_source=evento&portal_session=1");

    /// <summary>
    /// Y NO SE ACUMULA. Si cada vuelta añadiera la suya, a la tercera habría tres
    /// <c>portal_session</c> en la misma dirección y mandaría la más vieja.
    /// </summary>
    [Fact]
    public void LaMarca_SeReemplazaYNoSeAcumula() =>
        PortalSessionBounce
            .WithStep("https://alta.ejemplo.com/ambassador-join?portal_session=1&utm_source=evento", 2)
            .Should().Be(
                "https://alta.ejemplo.com/ambassador-join?utm_source=evento&portal_session=2");

    /// <summary>El destino de vuelta viaja escapado, que es lo que lo deja llegar entero.</summary>
    [Fact]
    public void ElDestinoDeVuelta_ViajaEscapadoEnLaDireccionDeSalida() =>
        PortalSessionBounce
            .SignOutUrlWithReturn(
                "https://portal.ejemplo.com/account/logout",
                "https://alta.ejemplo.com/ambassador-join/AMB-320189?portal_session=1")
            .Should().Be(
                "https://portal.ejemplo.com/account/logout?returnUrl=" +
                "https%3A%2F%2Falta.ejemplo.com%2Fambassador-join%2FAMB-320189%3Fportal_session%3D1");

    // ===============================================================================================
    //  Ayudas
    // ===============================================================================================

    private static string SoloLaQuery(string url)
    {
        var mark = url.IndexOf('?');
        return mark < 0 ? string.Empty : url[mark..];
    }

    /// <summary>
    /// La petición del rebote llegando al portal con la sesión de la persona anterior encima, y todo
    /// lo que hace falta para que el cierre pueda ocurrir de verdad: el servicio de autenticación que
    /// borra la cookie, el almacén de sesión con su entrada sembrada y un gateway apuntando a una API
    /// falsa.
    ///
    /// El gateway se monta con el proveedor de token DE VERDAD
    /// (<see cref="HttpContextAccessTokenProvider"/>), no con uno de mentira: así la prueba del
    /// refresh token comprueba también que el Bearer sale del principal de esta misma petición.
    /// </summary>
    private sealed class MundoDePruebas
    {
        public DefaultHttpContext   Contexto      { get; }
        public AutenticacionFalsa   Autenticacion { get; } = new();
        public PortalSessionTokens  Almacen       { get; }
        public ApiFalsa             Api           { get; } = new();
        public AuthApiGateway       Gateway       { get; }

        public MundoDePruebas(string? token, bool conSesion = true)
        {
            Contexto = Navegacion(token, conSesion);

            Almacen = new PortalSessionTokens(
                new AuthTokenRefresher(
                    new FabricaDeClientes(new ApiCaida()),
                    NullLogger<AuthTokenRefresher>.Instance),
                NullLogger<PortalSessionTokens>.Instance);

            // La sesión de la persona anterior, sembrada como la deja la puerta al firmar.
            if (conSesion)
                Almacen.Seed(SesionSembrada, new SessionTokens(token ?? string.Empty, "el-refresco"));

            Gateway = new AuthApiGateway(
                new FabricaDeClientes(Api),
                new HttpContextAccessTokenProvider(new AccesorFijo(Contexto), Almacen),
                NullLogger<AuthApiGateway>.Instance);

            Contexto.RequestServices = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IAuthenticationService>(Autenticacion)
                .AddSingleton(Gateway)
                .BuildServiceProvider();
        }

        /// <summary>Llama a la salida del portal de verdad y devuelve a dónde mandó al navegador.</summary>
        public async Task<string> SalirAsync(
            string? returnUrl = null, string? reason = null, AuthPortalOptions? portal = null)
        {
            var resultado = await AuthEndpoints.LogoutAsync(
                Contexto, Gateway, portal ?? CentroDeNegocios, Cookies, Almacen, reason, returnUrl);

            await resultado.ExecuteAsync(Contexto);

            return Contexto.Response.Headers.Location.ToString();
        }

        public IReadOnlyList<string> CookiesEscritas() =>
            Contexto.Response.Headers.SetCookie.Select(c => c ?? string.Empty).ToList();
    }

    /// <summary>El identificador de la sesión sembrada, el mismo que lleva el claim de la cookie.</summary>
    private const string SesionSembrada = "la-sesion-de-la-persona-anterior";

    /// <summary>La petición del rebote, con la sesión que se le diga.</summary>
    private static DefaultHttpContext Navegacion(string? token, bool conSesion)
    {
        var contexto = new DefaultHttpContext();

        if (conSesion)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "la-persona-anterior@ejemplo.com"),
                new(SessionExpiry.SessionIdClaim, SesionSembrada)
            };

            if (token is not null)
            {
                claims.Add(new Claim(SessionExpiry.AccessTokenClaim, token));
                claims.Add(new Claim(SessionExpiry.RefreshTokenClaim, "el-refresco"));
            }

            contexto.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie"));
        }

        contexto.Request.Method         = HttpMethods.Get;
        contexto.Request.Path           = "/account/logout";
        contexto.Request.Headers.Accept = "text/html,application/xhtml+xml";

        return contexto;
    }

    /// <summary>Cuenta las salidas: es lo que dice si la cookie de sesión se limpió de verdad.</summary>
    private sealed class AutenticacionFalsa : IAuthenticationService
    {
        public int Salidas { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(
            HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(
            HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(
            HttpContext context, string? scheme, ClaimsPrincipal principal,
            AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(
            HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            Salidas++;
            return Task.CompletedTask;
        }
    }

    /// <summary>SignupAPI, apuntando qué se le pidió y con qué credencial.</summary>
    private sealed class ApiFalsa : HttpMessageHandler
    {
        public List<(string Metodo, string Path, string? Autorizacion)> Peticiones { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Peticiones.Add((
                request.Method.Method,
                request.RequestUri?.AbsolutePath ?? string.Empty,
                request.Headers.Authorization?.ToString()));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"success":true}""", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>La API de renovación, que aquí nunca puede renovar nada.</summary>
    private sealed class ApiCaida : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
    }

    private sealed class FabricaDeClientes(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://signupapi.pruebas/")
            };
    }

    /// <summary>Un accesorio con el contexto de esta prueba y nada de estado compartido.</summary>
    private sealed class AccesorFijo(HttpContext contexto) : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = contexto;
    }

    // ── Tokens ──────────────────────────────────────────────────────────────────────────────────

    private static string TokenVivo() => Token(DateTime.UtcNow.AddMinutes(15));

    /// <summary>
    /// Un JWT sin firmar. Nada de este camino comprueba la firma —la comprobó la API al emitirlo—,
    /// así que basta con que se pueda leer y con que su <c>exp</c> diga la verdad.
    /// </summary>
    private static string Token(DateTime caduca) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer:    "pruebas",
            audience:  "pruebas",
            claims:    [
                new Claim(JwtRegisteredClaimNames.Sub,   "la-persona-anterior"),
                new Claim(JwtRegisteredClaimNames.Email, "la-persona-anterior@ejemplo.com")
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires:   caduca));
}
