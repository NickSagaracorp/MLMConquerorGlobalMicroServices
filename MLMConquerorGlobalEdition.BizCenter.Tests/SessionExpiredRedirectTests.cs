using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Resources;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.ClientCore;
using MLMConquerorGlobalEdition.SharedComponents.Resources;
using MLMConquerorGlobalEdition.SharedComponents.Server.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// Una sesión caducada tiene que acabar en la pantalla de login con su aviso, en los dos portales.
///
/// ESTE ARCHIVO EXISTE PORQUE ESO NO PASABA Y NADIE SE ENTERÓ DURANTE MESES. El manejador que lleva
/// el JWT a las APIs ya intentaba navegar al login, pero <c>IHttpClientFactory</c> lo construye en un
/// ÁMBITO DE DI PROPIO: el <c>NavigationManager</c> que recibía por el constructor no era el de la
/// pantalla del usuario, su <c>NavigateTo</c> lanzaba "'RemoteNavigationManager' has not been
/// initialized", y se lo tragaba su propio <c>catch</c>. El resultado, en la cara del usuario:
/// "Error loading countries: Response status code does not indicate success: 401 (Unauthorized)".
///
/// Es un fallo que el compilador no puede ver —todo compila, todo se inyecta, todo devuelve
/// verde— y que solo aparece con un token caducado dentro de un circuito. Así que lo que se prueba
/// aquí no es "el manejador llama a NavigateTo", que ya lo hacía, sino LO QUE FALLABA: que el
/// <c>NavigationManager</c> sobre el que navega es el del CIRCUITO y no el de su propio ámbito.
///
/// Los cuatro grupos, y qué guarda cada uno:
///   1. El manejador dentro del circuito — la causa raíz.
///   2. El middleware — el otro camino, el de las navegaciones HTTP (recargas, marcadores, el
///      primer render de un circuito recién abierto, que no es actividad entrante).
///   3. La salida con motivo — lo único que puede limpiar la cookie desde un circuito.
///   4. Los avisos de la pantalla de login — que TODO código que la puerta ponga en la URL tenga un
///      mensaje traducido. Ese es el segundo fallo que se cierra: SERVICE_UNAVAILABLE se emitía
///      desde que el login pasó por el gateway y ninguna de las dos pantallas lo traducía.
/// </summary>
public class SessionExpiredRedirectTests
{
    private const string LoginAdmin     = "/admin/login";
    private const string LoginBizCenter = "/login";
    private const string SalidaDelPortal = "/account/logout";

    private static readonly ChallengeCookieNames Cookies = new()
    {
        Login      = "mlm_pruebas_2fa_challenge",
        Enrollment = "mlm_pruebas_2fa_enrollment",
        Phone      = "mlm_pruebas_phone_challenge"
    };

    private static readonly AuthPortalOptions Admin = new()
    {
        LoginPage               = LoginAdmin,
        TwoFactorPage           = "/admin/login-2fa",
        EnrollAuthenticatorPage = "/admin/enroll-authenticator",
        HomePage                = "/admin",
        AllowedRoles            = ["SuperAdmin", "Admin"]
    };

    private static readonly AuthPortalOptions BizCenter = new()
    {
        LoginPage               = LoginBizCenter,
        TwoFactorPage           = "/two-factor",
        EnrollAuthenticatorPage = "/enroll-authenticator",
        HomePage                = "/"
    };

    public static TheoryData<string> LosDosPortales() => new() { "admin", "bizcenter" };

    private static AuthPortalOptions Portal(string nombre) =>
        nombre == "admin" ? Admin : BizCenter;

    // ===========================================================================================
    //  1. El manejador dentro del circuito — la causa raíz
    // ===========================================================================================

    /// <summary>
    /// LA PRUEBA DE LA CAUSA RAÍZ. El manejador se resuelve desde un ámbito distinto al del
    /// circuito, exactamente como lo hace <c>IHttpClientFactory</c>, y aun así la navegación tiene
    /// que salir por el <c>NavigationManager</c> DEL CIRCUITO.
    ///
    /// Si alguien vuelve a inyectar el <c>NavigationManager</c> por el constructor, esta prueba se
    /// pone roja: la navegación aparecería en el navegador del ámbito de la fábrica, que es el que
    /// no lleva a ninguna parte.
    /// </summary>
    [Fact]
    public async Task ConLaSesionCaducada_NavegaConElNavegadorDelCircuitoYNoConElDeSuAmbito()
    {
        var mundo = new MundoDePruebas(LoginAdmin);

        var respuesta = await mundo.LlamarAsync(TokenCaducado());

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "un token caducado ni siquiera se gasta en salir a la red");

        mundo.NavegadorDelCircuito.Navegaciones.Should().ContainSingle()
            .Which.Should().Be((SessionExpiry.LogoutUrl(SalidaDelPortal), true),
                "desde el circuito solo la salida del portal puede limpiar la cookie, y hace falta " +
                "una carga completa del navegador para que el componente en vuelo no llegue a " +
                "pintar su 401");

        mundo.NavegadorDeLaFabrica.Navegaciones.Should().BeEmpty(
            "el NavigationManager del ámbito en el que IHttpClientFactory arma la cadena no es el " +
            "de ninguna pantalla: navegar por ahí es lo que llevaba años sin hacer nada");
    }

    /// <summary>
    /// La API responde 401 aunque el token pareciera vivo —revocado, firmado con otra clave, reloj
    /// desviado—. El final tiene que ser el mismo.
    /// </summary>
    [Fact]
    public async Task ConUn401DeLaApi_TambienAcabaEnLaSalidaDelPortal()
    {
        var mundo = new MundoDePruebas(LoginAdmin, respuestaDeLaApi: HttpStatusCode.Unauthorized);

        await mundo.LlamarAsync(TokenVivo());

        mundo.LlamadasALaApi.Should().Be(1, "el token parecía bueno, así que se intentó de verdad");
        mundo.NavegadorDelCircuito.Navegaciones.Should().ContainSingle()
            .Which.Uri.Should().Be(SessionExpiry.LogoutUrl(SalidaDelPortal));
    }

    /// <summary>El camino feliz no se toca: Bearer puesto y nadie navega a ninguna parte.</summary>
    [Fact]
    public async Task ConLaSesionViva_AdjuntaElBearerYNoNavega()
    {
        var mundo = new MundoDePruebas(LoginAdmin);
        var token = TokenVivo();

        var respuesta = await mundo.LlamarAsync(token);

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK);
        mundo.AutorizacionRecibida.Should().Be($"Bearer {token}");
        mundo.NavegadorDelCircuito.Navegaciones.Should().BeEmpty();
        mundo.NavegadorDeLaFabrica.Navegaciones.Should().BeEmpty();
    }

    /// <summary>
    /// EL CASO DE <c>Members.razor</c>. Sin <c>HttpContext</c> a mano —las lecturas de un grid de
    /// Syncfusion son uno de los contextos donde no lo hay— el token tiene que salir del proveedor
    /// de estado DEL CIRCUITO.
    ///
    /// Antes se le preguntaba al proveedor del ámbito de la fábrica, que está vacío, la llamada
    /// salía sin Bearer y volvía con 401. Esa era la razón del apaño que llevaba esa pantalla
    /// —adjuntar el Bearer a mano desde el AuthenticationState en cascada—, y por eso se ha podido
    /// quitar.
    /// </summary>
    [Fact]
    public async Task SinHttpContext_SacaElTokenDelProveedorDeEstadoDelCircuito()
    {
        var mundo = new MundoDePruebas(LoginAdmin);
        var token = TokenVivo();

        await mundo.LlamarAsync(token);

        mundo.AutorizacionRecibida.Should().Be($"Bearer {token}",
            "el proveedor del circuito es el único que tiene el ClaimsPrincipal de la cookie " +
            "cuando la llamada no viene de una petición HTTP");
    }

    /// <summary>
    /// Sin circuito y con una respuesta HTTP que todavía no ha empezado —un render en servidor— el
    /// manejador hace el trabajo completo él mismo: cierra la sesión y redirige.
    /// </summary>
    [Fact]
    public async Task SinCircuito_CierraLaSesionYRedirigeSobreLaRespuestaEnCurso()
    {
        var mundo = new MundoDePruebas(LoginAdmin, conCircuito: false, conHttpContext: true);

        await mundo.LlamarAsync(TokenCaducado());

        mundo.Autenticacion.Salidas.Should().Be(1, "la cookie de sesión tiene que limpiarse");
        mundo.HttpContext!.Response.Headers.Location.ToString()
            .Should().Be($"{LoginAdmin}?error=session_expired");
    }

    /// <summary>
    /// El accesorio del circuito publica los servicios mientras dura la actividad entrante y los
    /// retira al terminar. Es la pieza que hace posible todo lo de arriba.
    /// </summary>
    [Fact]
    public async Task ElAccesorioDelCircuito_PublicaLosServiciosDeSuCircuitoSoloMientrasDuraLaActividad()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddPortalApiAuthHandler(LoginAdmin, SalidaDelPortal);

        using var raiz     = servicios.BuildServiceProvider();
        using var circuito = raiz.CreateScope();

        var accesorio = raiz.GetRequiredService<CircuitServicesAccessor>();
        accesorio.Services.Should().BeNull("fuera de un circuito no hay nada que publicar");

        // El manejador que registra AddPortalApiAuthHandler, construido desde el ámbito del
        // circuito, que es como lo construye Blazor.
        var manejador = circuito.ServiceProvider.GetRequiredService<CircuitHandler>();

        IServiceProvider? vistoDesdeDentro = null;
        var envuelto = manejador.CreateInboundActivityHandler(_ =>
        {
            vistoDesdeDentro = accesorio.Services;
            return Task.CompletedTask;
        });

        await envuelto(null!);

        vistoDesdeDentro.Should().BeSameAs(circuito.ServiceProvider,
            "es el proveedor de servicios de ESE circuito el que tiene que quedar a mano: es de " +
            "donde salen el NavigationManager y el AuthenticationStateProvider de la pantalla");

        accesorio.Services.Should().BeNull("al salir de la actividad entrante se retira");
    }

    // ===========================================================================================
    //  2. El middleware — las navegaciones HTTP
    // ===========================================================================================

    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Middleware_ConLaSesionCaducada_CierraLaSesionYMandaAlLoginDeSuPortal(
        string nombre)
    {
        var portal   = Portal(nombre);
        var contexto = Navegacion("/cualquier/pantalla", TokenCaducado());

        var siguiente = await Ejecutar(contexto, portal);

        siguiente.Should().BeFalse("la petición se corta aquí, no sigue al render");
        Autenticacion(contexto).Salidas.Should().Be(1);
        contexto.Response.Headers.Location.ToString()
            .Should().Be($"{portal.LoginPage}?error=session_expired");
    }

    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Middleware_ConLaSesionViva_NoToca(string nombre)
    {
        var contexto = Navegacion("/cualquier/pantalla", TokenVivo());

        var siguiente = await Ejecutar(contexto, Portal(nombre));

        siguiente.Should().BeTrue();
        Autenticacion(contexto).Salidas.Should().Be(0);
    }

    /// <summary>
    /// Redirigir al login desde el propio login es un bucle de redirecciones, y el usuario ni
    /// siquiera llegaría a ver el formulario para volver a entrar.
    /// </summary>
    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Middleware_NoTocaLasPantallasDeLaPuerta(string nombre)
    {
        var portal = Portal(nombre);

        foreach (var pagina in new[]
                 { portal.LoginPage, portal.TwoFactorPage, portal.EnrollAuthenticatorPage })
        {
            var contexto = Navegacion(pagina, TokenCaducado());
            var siguiente = await Ejecutar(contexto, portal);

            siguiente.Should().BeTrue($"{pagina} es de la puerta y se gobierna sola");
        }
    }

    /// <summary>
    /// Interceptar <c>/account/logout</c> sería impedir salir; interceptar el resto del área de
    /// cuenta sería pisar su propia política.
    /// </summary>
    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Middleware_NoTocaLosEndpointsDeCuenta(string nombre)
    {
        var contexto  = Navegacion("/account/logout", TokenCaducado());
        var siguiente = await Ejecutar(contexto, Portal(nombre));

        siguiente.Should().BeTrue(
            "la salida es justo a donde manda el circuito: cortarla dejaría la cookie sin limpiar");
    }

    /// <summary>
    /// A un WebSocket, a un recurso de <c>/_framework</c> o a una llamada de datos no se les puede
    /// redirigir: solo se les cortaría la conexión, sin aviso ninguno para el usuario.
    /// </summary>
    [Fact]
    public async Task Middleware_SoloActuaSobreNavegacionesDelNavegador()
    {
        var noEsUnaNavegacion = Navegacion("/_blazor", TokenCaducado(), acepta: "*/*");
        (await Ejecutar(noEsUnaNavegacion, Admin)).Should().BeTrue();

        var noEsUnGet = Navegacion("/cualquier/pantalla", TokenCaducado());
        noEsUnGet.Request.Method = HttpMethods.Post;
        (await Ejecutar(noEsUnGet, Admin)).Should().BeTrue();
    }

    /// <summary>
    /// Una sesión sin el claim del token no la firmó la puerta de este portal, así que aquí no hay
    /// nada que juzgar. Actuar sobre ella sería echar a un usuario por un token que nunca tuvo.
    /// </summary>
    [Fact]
    public async Task Middleware_SinElClaimDelToken_NoToca()
    {
        var contexto = Navegacion("/cualquier/pantalla", token: null);

        (await Ejecutar(contexto, Admin)).Should().BeTrue();
    }

    [Fact]
    public async Task Middleware_ConUsuarioAnonimo_NoToca()
    {
        var contexto = new DefaultHttpContext { RequestServices = ServiciosDePeticion() };
        contexto.Request.Method = HttpMethods.Get;
        contexto.Request.Path   = "/cualquier/pantalla";
        contexto.Request.Headers.Accept = "text/html";

        (await Ejecutar(contexto, Admin)).Should().BeTrue();
    }

    // ===========================================================================================
    //  3. La salida con motivo
    // ===========================================================================================

    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Salida_ConElMotivoDeCaducidad_LlevaElAvisoALaPantallaDeLogin(string nombre)
    {
        var portal   = Portal(nombre);
        var contexto = ContextoDeSalida();

        var resultado = await AuthEndpoints.LogoutAsync(
            contexto, GatewayCaido(), portal, Cookies, Almacen(), SessionExpiry.ErrorCode);
        await resultado.ExecuteAsync(contexto);

        contexto.Response.Headers.Location.ToString()
            .Should().Be($"{portal.LoginPage}?error=session_expired");
    }

    /// <summary>Una salida normal sigue yendo al login a secas, sin acusar a nadie de nada.</summary>
    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Salida_SinMotivo_VaAlLoginSinAviso(string nombre)
    {
        var portal   = Portal(nombre);
        var contexto = ContextoDeSalida();

        var resultado = await AuthEndpoints.LogoutAsync(
            contexto, GatewayCaido(), portal, Cookies, Almacen());
        await resultado.ExecuteAsync(contexto);

        contexto.Response.Headers.Location.ToString().Should().Be(portal.LoginPage);
    }

    /// <summary>
    /// El motivo lo pone el navegador, así que viene del usuario. Solo se reconoce el único valor
    /// conocido; cualquier otra cosa se ignora en vez de acabar reflejada en la URL del login.
    /// </summary>
    [Theory]
    [InlineData("cualquier-cosa")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("SESSION_EXPIRED")]
    public async Task Salida_ConUnMotivoDesconocido_NoLoRefleja(string motivo)
    {
        var contexto  = ContextoDeSalida();
        var resultado = await AuthEndpoints.LogoutAsync(
            contexto, GatewayCaido(), Admin, Cookies, Almacen(), motivo);
        await resultado.ExecuteAsync(contexto);

        contexto.Response.Headers.Location.ToString().Should().Be(Admin.LoginPage);
    }

    // ===========================================================================================
    //  4. Los avisos de la pantalla de login
    // ===========================================================================================

    /// <summary>
    /// EL SEGUNDO FALLO QUE SE CIERRA. La puerta redirige con <c>?error=SERVICE_UNAVAILABLE</c>
    /// cuando SignupAPI no responde, y ninguna de las dos pantallas de login lo traducía: el usuario
    /// veía el formulario otra vez, sin un solo aviso, y volvía a probar sus credenciales buenas
    /// contra un servicio caído.
    ///
    /// La prueba no comprueba una lista escrita a mano: hace fallar de verdad a los manejadores de la
    /// puerta, saca el código que ponen en la URL y exige que la pantalla de login sepa enseñarlo. Un
    /// código nuevo en el servidor sin su mensaje vuelve a poner esto en rojo.
    /// </summary>
    [Fact]
    public async Task TodoCodigoQueLaPuertaPoneEnLaUrl_TieneMensajeEnLaPantallaDeLogin()
    {
        var codigos = await CodigosQueEmiteLaPuertaAsync();

        codigos.Should().Contain(AuthApiGateway.Unreachable,
            "si SignupAPI cae, la puerta redirige con este código: es el que faltaba por traducir");
        codigos.Should().Contain(LoginErrorMessages.SessionExpired);
        codigos.Should().Contain(LoginErrorMessages.Invalid);
        codigos.Should().Contain(LoginErrorMessages.AccessDenied);

        foreach (var codigo in codigos)
        {
            LoginErrorMessages.For(codigo).Should().NotBeNull(
                $"la pantalla de login no sabe qué enseñar cuando la puerta redirige con " +
                $"?error={codigo}, así que el usuario se queda sin explicación");
        }
    }

    /// <summary>
    /// Cada código conocido tiene texto en inglés y en español. El resto de los nueve idiomas cae a
    /// inglés, que es el criterio con el que se han venido añadiendo las plantillas nuevas.
    /// </summary>
    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void CadaAvisoDeLogin_TieneTextoEnInglesYEnEspanol(string idioma)
    {
        var recursos = new ResourceManager(
            "MLMConquerorGlobalEdition.SharedComponents.Resources.SharedResources",
            typeof(SharedResources).Assembly);

        var cultura = new CultureInfo(idioma);

        foreach (var codigo in LoginErrorMessages.AllCodes)
        {
            var clave = LoginErrorMessages.For(codigo)!.ResourceKey;

            recursos.GetString(clave, cultura).Should().NotBeNullOrWhiteSpace(
                $"{clave} no tiene texto en {idioma}, así que el aviso saldría con la clave en crudo");
        }
    }

    /// <summary>
    /// Un código que esta versión de la interfaz no conoce no pinta nada: mejor callar que enseñar
    /// un literal del protocolo en la cara del usuario.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("un_codigo_que_no_existe")]
    public void UnCodigoDesconocido_NoPintaAviso(string? codigo)
    {
        LoginErrorMessages.For(codigo).Should().BeNull();
    }

    // ===========================================================================================
    //  Ayudas
    // ===========================================================================================

    /// <summary>
    /// El mundo del manejador: un ámbito para la fábrica de clientes HTTP y otro para el circuito,
    /// que es exactamente la separación que producía el fallo.
    /// </summary>
    private sealed class MundoDePruebas
    {
        private readonly ServiceProvider _raiz;
        private readonly IServiceScope   _ambitoDeLaFabrica;
        private readonly IServiceScope   _ambitoDelCircuito;
        private readonly ApiAuthHandler  _manejador;

        public NavegadorFalso     NavegadorDelCircuito { get; }
        public NavegadorFalso     NavegadorDeLaFabrica { get; }
        public AutenticacionFalsa Autenticacion        { get; } = new();
        public DefaultHttpContext? HttpContext         { get; }
        public string?            AutorizacionRecibida { get; private set; }
        public int                LlamadasALaApi       { get; private set; }

        public MundoDePruebas(
            string          loginPage,
            HttpStatusCode  respuestaDeLaApi = HttpStatusCode.OK,
            bool            conCircuito      = true,
            bool            conHttpContext   = false)
        {
            var servicios = new ServiceCollection();
            servicios.AddLogging();

            // El registro REAL del portal: si cambia, esta prueba lo ejerce.
            servicios.AddPortalApiAuthHandler(loginPage, SalidaDelPortal);

            // Uno por ámbito, que es lo que hace un portal de verdad.
            servicios.AddScoped<NavigationManager>(_ => new NavegadorFalso());
            servicios.AddScoped<AuthenticationStateProvider>(_ => new EstadoDelCircuito());

            _raiz              = servicios.BuildServiceProvider();
            _ambitoDeLaFabrica = _raiz.CreateScope();
            _ambitoDelCircuito = _raiz.CreateScope();

            NavegadorDeLaFabrica = (NavegadorFalso)_ambitoDeLaFabrica.ServiceProvider
                .GetRequiredService<NavigationManager>();
            NavegadorDelCircuito = (NavegadorFalso)_ambitoDelCircuito.ServiceProvider
                .GetRequiredService<NavigationManager>();

            if (conCircuito)
            {
                _raiz.GetRequiredService<CircuitServicesAccessor>().Services =
                    _ambitoDelCircuito.ServiceProvider;
            }

            if (conHttpContext)
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = new ServiceCollection()
                        .AddSingleton<IAuthenticationService>(Autenticacion)
                        .BuildServiceProvider()
                };
                _raiz.GetRequiredService<IHttpContextAccessor>().HttpContext = HttpContext;
            }

            // Se resuelve desde el ámbito de la FÁBRICA, igual que hace IHttpClientFactory.
            _manejador = _ambitoDeLaFabrica.ServiceProvider.GetRequiredService<ApiAuthHandler>();
            _manejador.InnerHandler = new ApiFalsa(peticion =>
            {
                LlamadasALaApi++;
                AutorizacionRecibida = peticion.Headers.Authorization?.ToString();
                return new HttpResponseMessage(respuestaDeLaApi);
            });
        }

        /// <summary>Una llamada a la API con el token del usuario ya puesto en su sitio.</summary>
        public async Task<HttpResponseMessage> LlamarAsync(string token)
        {
            ((EstadoDelCircuito)_ambitoDelCircuito.ServiceProvider
                .GetRequiredService<AuthenticationStateProvider>()).Token = token;

            // Cuando hay petición HTTP, el token está donde lo deja la cookie de sesión.
            if (HttpContext is not null)
                HttpContext.User = PrincipalCon(token);

            using var invocador = new HttpMessageInvoker(_manejador, disposeHandler: false);
            return await invocador.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://api.pruebas/algo"), default);
        }
    }

    /// <summary>Apunta a dónde se navega en vez de hablar con un navegador que aquí no existe.</summary>
    private sealed class NavegadorFalso : NavigationManager
    {
        public List<(string Uri, bool ForceLoad)> Navegaciones { get; } = [];

        public NavegadorFalso() => Initialize("https://portal.pruebas/", "https://portal.pruebas/");

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            Navegaciones.Add((uri, options.ForceLoad));
    }

    /// <summary>El proveedor de estado del circuito, con el ClaimsPrincipal de la cookie.</summary>
    private sealed class EstadoDelCircuito : AuthenticationStateProvider
    {
        public string? Token { get; set; }

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(PrincipalCon(Token)));
    }

    /// <summary>El usuario tal y como sale de la cookie de sesión: con su JWT como claim.</summary>
    private static ClaimsPrincipal PrincipalCon(string? token)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "quien@ejemplo.com") };
        if (token is not null) claims.Add(new Claim("access_token", token));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "pruebas"));
    }

    private sealed class ApiFalsa(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
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

    /// <summary>Una navegación del navegador con la sesión que se le diga.</summary>
    private static DefaultHttpContext Navegacion(
        string ruta, string? token, string acepta = "text/html,application/xhtml+xml")
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "quien@ejemplo.com") };
        if (token is not null) claims.Add(new Claim("access_token", token));

        var contexto = new DefaultHttpContext
        {
            RequestServices = ServiciosDePeticion(),
            User            = new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie"))
        };

        contexto.Request.Method         = HttpMethods.Get;
        contexto.Request.Path           = ruta;
        contexto.Request.Headers.Accept = acepta;

        return contexto;
    }

    /// <summary>
    /// Lo justo para que un <see cref="IResult"/> pueda ejecutarse sobre el contexto: el registro
    /// que pide la redirección y el servicio de autenticación que pide SignOutAsync.
    /// </summary>
    private static IServiceProvider ServiciosDePeticion() =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton<IAuthenticationService>(new AutenticacionFalsa())
            .BuildServiceProvider();

    private static AutenticacionFalsa Autenticacion(HttpContext contexto) =>
        (AutenticacionFalsa)contexto.RequestServices.GetRequiredService<IAuthenticationService>();

    /// <summary>Corre el middleware y dice si la petición siguió su camino.</summary>
    private static async Task<bool> Ejecutar(HttpContext contexto, AuthPortalOptions portal)
    {
        var siguio = false;

        var middleware = new SessionExpiryMiddleware(
            _ => { siguio = true; return Task.CompletedTask; },
            portal,
            Almacen(),
            NullLogger<SessionExpiryMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);
        return siguio;
    }

    private static DefaultHttpContext ContextoDeSalida() =>
        new() { RequestServices = ServiciosDePeticion() };

    /// <summary>
    /// Hace fallar de verdad a los manejadores de la puerta y recoge los códigos que acaban en la
    /// URL del login. Es lo contrario de una lista escrita a mano: si mañana la puerta emite un
    /// código nuevo, aparece aquí solo.
    /// </summary>
    private static async Task<HashSet<string>> CodigosQueEmiteLaPuertaAsync()
    {
        var codigos = new HashSet<string>(StringComparer.Ordinal);

        // SignupAPI caída.
        codigos.Add(await CodigoDe(async contexto => await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            GatewayCaido(), contexto, Admin, Cookies, Almacen(), default)));

        // Credenciales que no valen.
        codigos.Add(await CodigoDe(async contexto => await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            GatewayQueResponde("""{"success":false,"errorCode":"INVALID_CREDENTIALS"}"""),
            contexto, Admin, Cookies, Almacen(), default)));

        // Cuenta buena, rol que este portal no admite.
        codigos.Add(await CodigoDe(async contexto => await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            GatewayQueResponde(
                "{\"success\":true,\"data\":{\"accessToken\":\"" + TokenDeMiembro() + "\"}}"),
            contexto, Admin, Cookies, Almacen(), default)));

        // Reto del segundo factor gastado.
        codigos.Add(await CodigoDe(async contexto => await AuthEndpoints.LoginTwoFactorAsync(
            new AuthEndpoints.CodeForm("123456"),
            GatewayQueResponde("""{"success":false,"errorCode":"CODE_EXPIRED"}"""),
            contexto, Admin, Cookies, Almacen(), default), conRetoEnCookie: true));

        // La salida por caducidad, que es el camino nuevo.
        codigos.Add(await CodigoDe(async contexto => await AuthEndpoints.LogoutAsync(
            contexto, GatewayCaido(), Admin, Cookies, Almacen(), SessionExpiry.ErrorCode)));

        codigos.Remove(string.Empty);
        return codigos;
    }

    /// <summary>El valor de <c>?error=</c> de la redirección que produce un manejador.</summary>
    private static async Task<string> CodigoDe(
        Func<HttpContext, Task<IResult>> manejador, bool conRetoEnCookie = false)
    {
        var contexto = ContextoDeSalida();
        if (conRetoEnCookie)
            contexto.Request.Headers.Cookie = $"{Cookies.Login}=un-reto";

        var resultado = await manejador(contexto);
        await resultado.ExecuteAsync(contexto);

        var destino = contexto.Response.Headers.Location.ToString();
        var marca   = destino.IndexOf("error=", StringComparison.Ordinal);

        return marca < 0 ? string.Empty : Uri.UnescapeDataString(destino[(marca + 6)..]);
    }

    /// <summary>
    /// Un almacén de sesión de usar y tirar cuyo renovador no puede renovar nada: el cliente
    /// responde 401 a todo. Es lo que hace falta en este archivo, que prueba qué pasa cuando la
    /// sesión está MUERTA de verdad; la renovación que sí sale bien se prueba en
    /// <c>RefrescoDeSesionTests</c>.
    /// </summary>
    private static PortalSessionTokens Almacen() =>
        new(new AuthTokenRefresher(
                new FabricaDeClientes(new ApiFalsa(
                    _ => new HttpResponseMessage(HttpStatusCode.Unauthorized))),
                NullLogger<AuthTokenRefresher>.Instance),
            NullLogger<PortalSessionTokens>.Instance);

    private static AuthApiGateway GatewayCaido() =>
        Gateway(new ApiFalsa(_ => throw new HttpRequestException("SignupAPI no responde")));

    private static AuthApiGateway GatewayQueResponde(string cuerpoJson) =>
        Gateway(new ApiFalsa(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(cuerpoJson, Encoding.UTF8, "application/json")
        }));

    private static AuthApiGateway Gateway(HttpMessageHandler handler) =>
        new(new FabricaDeClientes(handler), new SinToken(),
            NullLogger<AuthApiGateway>.Instance);

    private sealed class FabricaDeClientes(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://signupapi.pruebas/")
            };
    }

    private sealed class SinToken : IAccessTokenProvider
    {
        public ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<string?>(null);
    }

    // ── Tokens ──────────────────────────────────────────────────────────────────────────────────

    private static string TokenVivo()     => Token(DateTime.UtcNow.AddMinutes(15));
    private static string TokenCaducado() => Token(DateTime.UtcNow.AddMinutes(-1));

    private static string TokenDeMiembro() => Token(DateTime.UtcNow.AddMinutes(15), "Member");

    /// <summary>
    /// Un JWT sin firmar. Nada del camino comprueba la firma —la comprobó la API al emitirlo—, así
    /// que basta con que se pueda leer y con que su <c>exp</c> diga la verdad.
    /// </summary>
    private static string Token(DateTime caduca, string rol = "SuperAdmin") =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer:    "pruebas",
            audience:  "pruebas",
            claims:    [
                new Claim(JwtRegisteredClaimNames.Sub,   "un-usuario"),
                new Claim(JwtRegisteredClaimNames.Email, "quien@ejemplo.com"),
                new Claim(ClaimTypes.Role, rol)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires:   caduca));
}
