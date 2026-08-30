using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.ClientCore;
using MLMConquerorGlobalEdition.SharedComponents.Server.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// CARGAR LA PANTALLA DE ALTA TIENE QUE MATAR CUALQUIER SESIÓN ABIERTA EN ESE NAVEGADOR.
///
/// EL ESCENARIO, que es el que hay que tener en la cabeza leyendo este archivo: en un evento se dan
/// de alta varias personas seguidas en el mismo ordenador. La persona A termina y se levanta sin
/// salir; la persona B se sienta y abre el alta —normalmente por el enlace del sitio replicado de su
/// patrocinador, que es la ruta con slug—. Con la sesión de A viva, a B le basta con teclear
/// cualquier dirección del portal para estar dentro de la cuenta de A.
///
/// LO QUE SE PRUEBA AQUÍ NO ES "SE LLAMA A SignOutAsync". Eso lo haría igual de bien un
/// <c>OnInitializedAsync</c> en la página, y ahí NO FUNCIONARÍA: dentro de un circuito de Blazor
/// Server la respuesta a mano es la del WebSocket y ya empezó, así que la cookie de A seguiría en el
/// navegador de B con una línea de código encima que hace creer que está resuelto. Lo que se prueba
/// son las CUATRO cosas que tienen que morir —la cookie de sesión, las tres cookies de reto, la
/// entrada del almacén y el refresh token EN LA API—, que el alta sigue funcionando para quien llega
/// sin sesión, y el orden en la tubería, que es lo que decide si el usuario acaba en el alta o en el
/// login.
/// </summary>
public class CierreDeSesionAlAbrirElAltaTests
{
    private const string Alta  = "/signup";
    private const string Login = "/login";

    private static readonly ChallengeCookieNames Cookies = new()
    {
        Login      = "mlm_pruebas_2fa_challenge",
        Enrollment = "mlm_pruebas_2fa_enrollment",
        Phone      = "mlm_pruebas_phone_challenge"
    };

    /// <summary>El centro de negocios: el único portal con pantalla de alta.</summary>
    private static readonly AuthPortalOptions CentroDeNegocios = new()
    {
        LoginPage               = Login,
        TwoFactorPage           = "/two-factor",
        EnrollAuthenticatorPage = "/enroll-authenticator",
        HomePage                = "/",
        SignupPage              = Alta
    };

    /// <summary>Administración, que no tiene alta y por eso no declara la ruta.</summary>
    private static readonly AuthPortalOptions Administracion = new()
    {
        LoginPage               = "/admin/login",
        TwoFactorPage           = "/admin/login-2fa",
        EnrollAuthenticatorPage = "/admin/enroll-authenticator",
        HomePage                = "/admin"
    };

    // ===========================================================================================
    //  1. Las cuatro cosas que tienen que morir
    // ===========================================================================================

    /// <summary>
    /// LA PRUEBA DEL ESCENARIO. Se abre el alta con la sesión de la persona anterior viva: la cookie
    /// de sesión se limpia y la petición no llega a la página, porque se redirige.
    /// </summary>
    [Fact]
    public async Task AbrirElAlta_ConLaSesionDeOtroViva_LimpiaLaCookieDeSesion()
    {
        var mundo = new MundoDePruebas(Alta, TokenVivo());

        var llegoALaPagina = await mundo.EjecutarAsync();

        mundo.Autenticacion.Salidas.Should().Be(1,
            "la cookie de sesión de la persona anterior es lo primero que tiene que desaparecer");
        llegoALaPagina.Should().BeFalse("se redirige con el aviso, así que esta petición acaba aquí");
    }

    /// <summary>
    /// LA RUTA QUE DE VERDAD SE USA EN UN EVENTO: el enlace del sitio replicado del patrocinador.
    /// Cubrir solo <c>/signup</c> dejaría el agujero abierto justo por donde entra la gente.
    /// </summary>
    [Fact]
    public async Task AbrirElAltaConElSlugDelPatrocinador_TambienMataLaSesion()
    {
        var mundo = new MundoDePruebas("/signup/JUANPEREZ", TokenVivo());

        await mundo.EjecutarAsync();

        mundo.Autenticacion.Salidas.Should().Be(1);
        mundo.Destino.Should().Be("/signup/JUANPEREZ?session_closed=1",
            "el patrocinador tiene que llegar entero al asistente de alta");
    }

    /// <summary>
    /// Las TRES cookies de reto, no dos. Un segundo factor a medias o un alta de teléfono a medias
    /// de la persona anterior son credenciales de un solo paso que la siguiente podría canjear.
    /// </summary>
    [Fact]
    public async Task MataLasTresCookiesDeReto()
    {
        var mundo = new MundoDePruebas(Alta, TokenVivo());

        await mundo.EjecutarAsync();

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
    public async Task InvalidaElRefreshTokenEnLaApi_ConElTokenDelUsuarioPuesto()
    {
        var token = TokenVivo();
        var mundo = new MundoDePruebas(Alta, token);

        await mundo.EjecutarAsync();

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
    public async Task OlvidaLaEntradaDelAlmacenDeSesion()
    {
        var mundo = new MundoDePruebas(Alta, TokenVivo());
        mundo.Almacen.Count.Should().Be(1, "la sesión de la persona anterior estaba sembrada");

        await mundo.EjecutarAsync();

        mundo.Almacen.Count.Should().Be(0);
    }

    /// <summary>
    /// Y EL USUARIO DE ESTA PETICIÓN. <c>SignOutAsync</c> escribe una cabecera para el navegador; no
    /// toca lo que el resto de la tubería ya tiene en la mano. Sin esto, el middleware siguiente, la
    /// autorización y el apretón de manos del circuito seguirían viendo a la persona anterior.
    /// </summary>
    [Fact]
    public async Task DejaAnonimoAlUsuarioDeEstaPeticion()
    {
        var mundo = new MundoDePruebas(Alta, TokenVivo());

        await mundo.EjecutarAsync();

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
        var mundo = new MundoDePruebas(Alta, token: null);

        await mundo.EjecutarAsync();

        mundo.Autenticacion.Salidas.Should().Be(1);
        mundo.Api.Peticiones.Should().BeEmpty(
            "sin token no hay nada que invalidar al otro lado, pero la sesión local se cierra igual");
    }

    // ===========================================================================================
    //  2. El alta sigue funcionando
    // ===========================================================================================

    /// <summary>
    /// EL CAMINO DE CASI TODO EL MUNDO: llegar al alta sin sesión ninguna. Ni se firma nada, ni se
    /// redirige, ni se avisa de nada.
    /// </summary>
    [Fact]
    public async Task SinSesion_ElAltaSeAbreComoSiempre()
    {
        var mundo = new MundoDePruebas(Alta, token: null, conSesion: false);

        var llegoALaPagina = await mundo.EjecutarAsync();

        llegoALaPagina.Should().BeTrue();
        mundo.Autenticacion.Salidas.Should().Be(0);
        mundo.Destino.Should().BeEmpty("sin sesión cerrada no hay nada que avisar");
    }

    /// <summary>
    /// Lo que no es el navegador CARGANDO la página no se toca: el WebSocket del circuito, los
    /// recursos de <c>/_framework</c> y las llamadas del propio asistente de alta. Cortar cualquiera
    /// de esos dejaría el alta a medias sin que el usuario supiera por qué.
    /// </summary>
    [Fact]
    public async Task NoTocaLoQueNoEsUnaNavegacionDelNavegador()
    {
        var noEsHtml = new MundoDePruebas(Alta, TokenVivo(), acepta: "*/*");
        (await noEsHtml.EjecutarAsync()).Should().BeTrue();
        noEsHtml.Autenticacion.Salidas.Should().Be(0);

        var noEsGet = new MundoDePruebas(Alta, TokenVivo(), metodo: HttpMethods.Post);
        (await noEsGet.EjecutarAsync()).Should().BeTrue();
        noEsGet.Autenticacion.Salidas.Should().Be(0);

        var elCircuito = new MundoDePruebas("/_blazor", TokenVivo(), acepta: "*/*");
        (await elCircuito.EjecutarAsync()).Should().BeTrue();
        elCircuito.Autenticacion.Salidas.Should().Be(0);
    }

    /// <summary>
    /// El resto del portal no se toca. Si esto fallara, un miembro no podría usar su centro de
    /// negocios: cada pantalla le cerraría la sesión.
    /// </summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/account")]
    [InlineData("/account/logout")]
    [InlineData(Login)]
    [InlineData("/team")]
    public async Task NoTocaNingunaOtraPantalla(string ruta)
    {
        var mundo = new MundoDePruebas(ruta, TokenVivo());

        (await mundo.EjecutarAsync()).Should().BeTrue();
        mundo.Autenticacion.Salidas.Should().Be(0);
    }

    /// <summary>
    /// Una ruta que empieza con las mismas letras no es la pantalla de alta. Se compara por
    /// segmentos justamente para esto.
    /// </summary>
    [Fact]
    public async Task NoConfundeUnaRutaQueSoloEmpiezaIgual()
    {
        var mundo = new MundoDePruebas("/signupdelotro", TokenVivo());

        (await mundo.EjecutarAsync()).Should().BeTrue();
        mundo.Autenticacion.Salidas.Should().Be(0);
    }

    /// <summary>
    /// El portal que no declara pantalla de alta —administración— no tiene aquí ninguna ruta que
    /// mirar, ni siquiera una que se llame igual.
    /// </summary>
    [Fact]
    public void UnPortalSinPantallaDeAlta_NoMiraNingunaRuta()
    {
        var contexto = Navegacion(Alta, TokenVivo(), HttpMethods.Get, "text/html", conSesion: true);

        SignupSessionResetMiddleware.IsSignupNavigation(contexto, Administracion.SignupPage)
            .Should().BeFalse();
    }

    /// <summary>
    /// Y montarlo en un portal que no la declaró falla AL ARRANCAR, no en silencio. Un middleware
    /// que no mira ninguna ruta es una protección apagada que parece encendida desde el
    /// <c>Program.cs</c>, y eso no se nota hasta que alguien mira las cookies de un navegador en un
    /// evento.
    /// </summary>
    [Fact]
    public void MontarloSinLaRutaDeAlta_FallaAlArrancar()
    {
        var servicios = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Administracion)
            .BuildServiceProvider();

        var app = new ApplicationBuilder(servicios);

        app.Invoking(a => a.UseSignupSessionReset())
           .Should().Throw<InvalidOperationException>()
           .WithMessage("*SignupPage*");
    }

    // ===========================================================================================
    //  3. El aviso al usuario, y que no haya bucle
    // ===========================================================================================

    /// <summary>
    /// La marca del aviso se añade CONSERVANDO lo que ya traía la dirección: el slug del
    /// patrocinador y la query de la campaña por la que llegó.
    /// </summary>
    [Fact]
    public async Task ElAviso_ConservaElSlugYLaQueQueYaTraia()
    {
        var mundo = new MundoDePruebas("/signup/JUANPEREZ", TokenVivo(), query: "?utm_source=evento");

        await mundo.EjecutarAsync();

        mundo.Destino.Should().Be("/signup/JUANPEREZ?utm_source=evento&session_closed=1");
    }

    /// <summary>
    /// EL BUCLE, QUE ES LO ÚNICO QUE PODRÍA DEJAR SIN ALTA A UNA SALA ENTERA. Si por lo que sea la
    /// cookie volviera a llegar después del aviso, la sesión se mata igual pero NO se redirige otra
    /// vez: se sigue a la página. Como mucho una redirección, pase lo que pase al otro lado.
    /// </summary>
    [Fact]
    public async Task ConElAvisoYaPuesto_MataLaSesionPeroNoRedirigeOtraVez()
    {
        var mundo = new MundoDePruebas(Alta, TokenVivo(), query: "?session_closed=1");

        var llegoALaPagina = await mundo.EjecutarAsync();

        mundo.Autenticacion.Salidas.Should().Be(1, "la sesión se mata igual, que es lo importante");
        llegoALaPagina.Should().BeTrue("y se sigue a la página en vez de redirigir por segunda vez");
        mundo.Destino.Should().BeEmpty();
    }

    // ===========================================================================================
    //  4. El orden en la tubería — lo que decide si el usuario acaba en el alta o en el login
    // ===========================================================================================

    /// <summary>
    /// MATAR LA SESIÓN AQUÍ NO ES "SESIÓN CADUCADA". Quien abre el alta quiere darse de alta, así
    /// que se queda en el alta aunque el JWT que traía estuviera caducado. Esta prueba monta los dos
    /// middlewares EN EL ORDEN REAL del portal.
    /// </summary>
    [Fact]
    public async Task ConElJwtCaducado_SeQuedaEnElAltaYNoAcabaEnElLogin()
    {
        var mundo = new MundoDePruebas(Alta, TokenCaducado());

        var llegoALaPagina = await mundo.EjecutarConLaTuberiaCompletaAsync();

        llegoALaPagina.Should().BeFalse();
        mundo.Destino.Should().Be("/signup?session_closed=1");
        mundo.Destino.Should().NotContain(Login,
            "acabar en el login sería cerrarle la única pantalla a la que venía");
        mundo.Destino.Should().NotContain("session_expired");
    }

    /// <summary>
    /// Y POR QUÉ ESE ORDEN Y NO EL OTRO. Con el middleware de caducidad delante, la misma persona
    /// —misma sesión, mismo token caducado— acaba en el login con el aviso de sesión caducada. La
    /// sesión moriría igual, pero el alta se le habría cerrado en la cara.
    ///
    /// Esta prueba existe para que ese orden no se pueda cambiar por descuido: es de las cosas que
    /// no fallan al compilar y no se ven leyendo el diff.
    /// </summary>
    [Fact]
    public async Task ConLosMiddlewaresAlReves_ElAltaSePierdeEnElLogin()
    {
        var mundo = new MundoDePruebas(Alta, TokenCaducado());

        await mundo.EjecutarConLaTuberiaAlRevesAsync();

        mundo.Destino.Should().Be($"{Login}?error=session_expired",
            "es exactamente lo que NO puede pasar, y por eso el orden del Program.cs importa");
    }

    // ===========================================================================================
    //  5. La salida de la puerta, que ahora comparte el cierre
    // ===========================================================================================

    /// <summary>
    /// La salida normal del portal sigue haciendo lo suyo, y ahora además se lleva el reto del
    /// teléfono, que antes sobrevivía a la sesión de su dueño.
    /// </summary>
    [Fact]
    public async Task LaSalidaDeLaPuerta_LimpiaLosTresRetosYSigueYendoAlLogin()
    {
        var mundo = new MundoDePruebas(Alta, TokenVivo());

        var resultado = await AuthEndpoints.LogoutAsync(
            mundo.Contexto, mundo.Gateway, CentroDeNegocios, Cookies, mundo.Almacen);
        await resultado.ExecuteAsync(mundo.Contexto);

        mundo.Contexto.Response.Headers.Location.ToString().Should().Be(Login);
        mundo.Autenticacion.Salidas.Should().Be(1);

        var escritas = mundo.CookiesEscritas();
        escritas.Should().Contain(c => c.StartsWith($"{Cookies.Login}="));
        escritas.Should().Contain(c => c.StartsWith($"{Cookies.Enrollment}="));
        escritas.Should().Contain(c => c.StartsWith($"{Cookies.Phone}="));

        mundo.Api.Peticiones.Should().ContainSingle()
            .Which.Path.Should().Be("/api/v1/auth/logout");
    }

    // ===========================================================================================
    //  Ayudas
    // ===========================================================================================

    /// <summary>
    /// Una petición del navegador con la sesión de la persona anterior encima, y todo lo que hace
    /// falta para que el cierre pueda ocurrir de verdad: el servicio de autenticación que borra la
    /// cookie, el almacén de sesión con su entrada sembrada y un gateway apuntando a una API falsa.
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

        public MundoDePruebas(
            string  ruta,
            string? token,
            string  acepta    = "text/html,application/xhtml+xml",
            string  metodo    = "GET",
            string  query     = "",
            bool    conSesion = true)
        {
            Contexto = Navegacion(ruta, token, metodo, acepta, conSesion, query);

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

        /// <summary>A dónde se redirigió, o cadena vacía si no hubo redirección.</summary>
        public string Destino => Contexto.Response.Headers.Location.ToString();

        public IReadOnlyList<string> CookiesEscritas() =>
            Contexto.Response.Headers.SetCookie.Select(c => c ?? string.Empty).ToList();

        /// <summary>Corre el middleware del alta y dice si la petición llegó a la página.</summary>
        public Task<bool> EjecutarAsync() => CorrerAsync(soloElDelAlta: true, alReves: false);

        /// <summary>El orden REAL del portal: primero el del alta, después el de caducidad.</summary>
        public Task<bool> EjecutarConLaTuberiaCompletaAsync() =>
            CorrerAsync(soloElDelAlta: false, alReves: false);

        /// <summary>El orden equivocado, para dejar por escrito lo que produce.</summary>
        public Task<bool> EjecutarConLaTuberiaAlRevesAsync() =>
            CorrerAsync(soloElDelAlta: false, alReves: true);

        private async Task<bool> CorrerAsync(bool soloElDelAlta, bool alReves)
        {
            var llegoALaPagina = false;
            RequestDelegate laPagina = _ => { llegoALaPagina = true; return Task.CompletedTask; };

            RequestDelegate DelAlta(RequestDelegate siguiente) =>
                new SignupSessionResetMiddleware(
                    siguiente, CentroDeNegocios, Cookies, Almacen,
                    NullLogger<SignupSessionResetMiddleware>.Instance).InvokeAsync;

            RequestDelegate DeCaducidad(RequestDelegate siguiente) =>
                new SessionExpiryMiddleware(
                    siguiente, CentroDeNegocios, Almacen,
                    NullLogger<SessionExpiryMiddleware>.Instance).InvokeAsync;

            var tuberia = soloElDelAlta
                ? DelAlta(laPagina)
                : alReves
                    ? DeCaducidad(DelAlta(laPagina))
                    : DelAlta(DeCaducidad(laPagina));

            await tuberia(Contexto);
            return llegoALaPagina;
        }
    }

    /// <summary>El identificador de la sesión sembrada, el mismo que lleva el claim de la cookie.</summary>
    private const string SesionSembrada = "la-sesion-de-la-persona-anterior";

    /// <summary>Una navegación del navegador con la sesión que se le diga.</summary>
    private static DefaultHttpContext Navegacion(
        string ruta, string? token, string metodo, string acepta, bool conSesion,
        string query = "")
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

        contexto.Request.Method         = metodo;
        contexto.Request.Path           = ruta;
        contexto.Request.QueryString    = new QueryString(query);
        contexto.Request.Headers.Accept = acepta;

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

    private static string TokenVivo()     => Token(DateTime.UtcNow.AddMinutes(15));
    private static string TokenCaducado() => Token(DateTime.UtcNow.AddMinutes(-1));

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
