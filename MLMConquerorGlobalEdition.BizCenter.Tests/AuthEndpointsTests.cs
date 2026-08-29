using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.ClientCore;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// La puerta de los dos portales: los manejadores compartidos de <see cref="AuthEndpoints"/>.
///
/// LO QUE DE VERDAD VIGILA ESTE ARCHIVO es el primer grupo de pruebas: que NINGÚN camino de la
/// puerta devuelva un 500 porque SignupAPI no responda. Ese fallo existió: los manejadores hacían
/// <c>PostAsJsonAsync</c> a pelo y sin <c>try</c>, así que con el servicio caído el login reventaba
/// en la cara del usuario mientras <c>/account/forgot-password</c> —que ya iba por
/// <see cref="AuthApiGateway"/>— respondía con una redirección y un código de error. Es un fallo que
/// vuelve solo en cuanto alguien escriba una llamada nueva sin pasar por el gateway, y por eso se
/// comprueba manejador a manejador y no una vez.
///
/// El segundo grupo cubre el otro fallo que se cerró al unificar: el centro de negocios no sabía
/// leer <c>RequiresEnrollment</c> y mandaba al usuario a <c>/login?error=invalid</c> —"tus
/// credenciales están mal"— cuando lo que pasaba es que le faltaba configurar el segundo factor.
///
/// El tercero comprueba que lo específico de cada portal sigue entrando por parámetro y no se ha
/// quedado escrito a fuego: los destinos, los roles admitidos, el nombre de la cookie del reto y el
/// nombre del parámetro con el destino enmascarado.
/// </summary>
public class AuthEndpointsTests
{
    // ===========================================================================================
    //  Los dos portales, tal y como los declara su Program.cs
    // ===========================================================================================

    private static readonly ChallengeCookieNames CookiesAdmin = new()
    {
        Login      = "mlm_admin_2fa_challenge",
        Enrollment = "mlm_admin_2fa_enrollment",
        Phone      = "mlm_admin_phone_challenge"
    };

    private static readonly ChallengeCookieNames CookiesBizCenter = new()
    {
        Login      = "mlm_bizcenter_2fa_challenge",
        Enrollment = "mlm_bizcenter_2fa_enrollment",
        Phone      = "mlm_bizcenter_phone_challenge"
    };

    private static readonly AuthPortalOptions Admin = new()
    {
        LoginPage               = "/admin/login",
        TwoFactorPage           = "/admin/login-2fa",
        EnrollAuthenticatorPage = "/admin/enroll-authenticator",
        HomePage                = "/admin",
        AllowedRoles            =
        [
            "SuperAdmin", "Admin", "CommissionManager",
            "BillingManager", "SupportManager",
            "SupportLevel1", "SupportLevel2", "SupportLevel3", "IT"
        ]
    };

    private static readonly AuthPortalOptions BizCenter = new()
    {
        LoginPage                 = "/login",
        TwoFactorPage             = "/two-factor",
        EnrollAuthenticatorPage   = "/enroll-authenticator",
        HomePage                  = "/",
        FollowsMemberLanguage     = true,
        TwoFactorTargetQueryParam = "email",
        TwoFactorErrorCode        = "invalid_code"
    };

    /// <summary>Los dos portales para las pruebas que valen igual en los dos.</summary>
    public static TheoryData<string> LosDosPortales() => new() { "admin", "bizcenter" };

    private static (AuthPortalOptions Portal, ChallengeCookieNames Cookies) Portal(string nombre) =>
        nombre == "admin" ? (Admin, CookiesAdmin) : (BizCenter, CookiesBizCenter);

    // ===========================================================================================
    //  1. El 500: con SignupAPI caída, ningún camino puede reventar
    // ===========================================================================================

    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Login_ConLaApiCaida_RedirigeConServiceUnavailableEnVezDeReventar(string nombre)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto();

        var resultado = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            GatewayCaido(), contexto, portal, cookies, default);

        var destino = await Ejecutar(resultado, contexto);

        contexto.Response.StatusCode.Should().Be(302,
            "una API caída tiene que salir como redirección con un mensaje, no como un 500");
        destino.Should().Be($"{portal.LoginPage}?error={AuthApiGateway.Unreachable}");
    }

    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task SegundoFactor_ConLaApiCaida_VuelveALaPantallaEnVezDeReventar(string nombre)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto(conCookie: (cookies.Login, "un-reto"));

        var resultado = await AuthEndpoints.LoginTwoFactorAsync(
            new AuthEndpoints.CodeForm("123456"),
            GatewayCaido(), contexto, portal, cookies, default);

        var destino = await Ejecutar(resultado, contexto);

        contexto.Response.StatusCode.Should().Be(302);
        destino.Should().StartWith($"{portal.TwoFactorPage}?error=",
            "el reto sigue vivo: lo que falló fue el transporte, así que el usuario tiene que " +
            "poder reintentar sin volver a empezar");
    }

    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Reenvio_ConLaApiCaida_VuelveALaPantallaEnVezDeReventar(string nombre)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto(conCookie: (cookies.Login, "un-reto"));

        var resultado = await AuthEndpoints.ResendTwoFactorAsync(
            GatewayCaido(), contexto, portal, cookies, default);

        var destino = await Ejecutar(resultado, contexto);

        contexto.Response.StatusCode.Should().Be(302);
        destino.Should().StartWith($"{portal.TwoFactorPage}?error=");
    }

    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Enrolamiento_ConLaApiCaida_VuelveALaPantallaEnVezDeReventar(string nombre)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto(conCookie: (cookies.Enrollment, "un-token"));

        var resultado = await AuthEndpoints.EnrollAuthenticatorAsync(
            new AuthEndpoints.CodeForm("123456"),
            GatewayCaido(), contexto, portal, cookies, default);

        var destino = await Ejecutar(resultado, contexto);

        contexto.Response.StatusCode.Should().Be(302);
        destino.Should().StartWith($"{portal.EnrollAuthenticatorPage}?error=");
    }

    /// <summary>
    /// El barrido: cualquiera de los cuatro manejadores, con el servicio caído, termina en una
    /// redirección. Si mañana alguien añade un quinto y lo escribe con <c>PostAsJsonAsync</c> a
    /// pelo, esto no lo ve — pero si toca uno de estos cuatro, sí.
    /// </summary>
    [Fact]
    public async Task NingunManejadorDeLaPuertaDejaEscaparLaExcepcionDeUnServicioCaido()
    {
        var cookies = CookiesBizCenter;

        var manejadores = new Func<HttpContext, Task<IResult>>[]
        {
            ctx => AuthEndpoints.LoginAsync(
                new AuthEndpoints.LoginForm("quien@ejemplo.com", "x"),
                GatewayCaido(), ctx, BizCenter, cookies, default),

            ctx => AuthEndpoints.LoginTwoFactorAsync(
                new AuthEndpoints.CodeForm("123456"),
                GatewayCaido(), ctx, BizCenter, cookies, default),

            ctx => AuthEndpoints.ResendTwoFactorAsync(
                GatewayCaido(), ctx, BizCenter, cookies, default),

            ctx => AuthEndpoints.EnrollAuthenticatorAsync(
                new AuthEndpoints.CodeForm("123456"),
                GatewayCaido(), ctx, BizCenter, cookies, default),
        };

        foreach (var manejador in manejadores)
        {
            var contexto = Contexto(conCookie: (cookies.Login, "un-reto"));
            contexto.Request.Headers.Cookie =
                $"{cookies.Login}=un-reto; {cookies.Enrollment}=un-token";

            var resultado = await manejador(contexto);
            await Ejecutar(resultado, contexto);

            contexto.Response.StatusCode.Should().Be(302);
        }
    }

    /// <summary>
    /// El otro cuerpo que no es JSON: el 429 que emite el limitador de tasa antes del pipeline MVC,
    /// o la página de error de un proxy intermedio. Deserializarlo lanza, y esa excepción también
    /// salía como 500.
    /// </summary>
    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Login_ConUnCuerpoQueNoEsJson_RedirigeEnVezDeReventar(string nombre)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto();

        var gateway = Gateway(new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("<html>Too many requests</html>", Encoding.UTF8, "text/html")
        }));

        var resultado = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "x"),
            gateway, contexto, portal, cookies, default);

        var destino = await Ejecutar(resultado, contexto);

        contexto.Response.StatusCode.Should().Be(302);
        destino.Should().Be($"{portal.LoginPage}?error=invalid");
    }

    /// <summary>
    /// Un POST sin ningún campo deja el parámetro del formulario en null y el manejador reventaba
    /// con una NullReferenceException — otro 500, este sin necesidad de que nada estuviera caído.
    /// </summary>
    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Login_ConElFormularioVacio_RedirigeEnVezDeReventar(string nombre)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto();

        var resultado = await AuthEndpoints.LoginAsync(
            form: null, GatewayCaido(), contexto, portal, cookies, default);

        await Ejecutar(resultado, contexto);
        contexto.Response.StatusCode.Should().Be(302);
    }

    /// <summary>
    /// El contraste que delataba el fallo: dentro de un mismo portal, la recuperación de contraseña
    /// —que ya iba por el gateway— respondía con SERVICE_UNAVAILABLE y el login con un 500. Ahora
    /// las dos puertas dicen lo mismo, porque las dos pasan por el mismo sitio.
    /// </summary>
    [Fact]
    public async Task ConLaApiCaidaElLoginDiceLoMismoQueLaRecuperacionDeContrasena()
    {
        var rutas = new AccountPageRoutes
        {
            ForgotPasswordPage     = "/login/forgot-password",
            ForgotPasswordSentPage = "/login/forgot-password/sent",
            ResetPasswordPage      = "/login/reset-password",
            ResetPasswordDonePage  = "/login/reset-password/done",
            ProfilePage            = "/account",
            PasswordPage           = "/account/password",
            PhonePage              = "/account/phone",
            PhoneVerifyPage        = "/account/phone/verify",
            PersonalDataPage       = "/account/personal-data"
        };

        var contextoRecuperacion = Contexto();
        var recuperacion = await AccountEndpoints.ForgotPasswordAsync(
            new AccountEndpoints.EmailForm("quien@ejemplo.com"), GatewayCaido(), rutas, default);
        var destinoRecuperacion = await Ejecutar(recuperacion, contextoRecuperacion);

        var contextoLogin = Contexto();
        var login = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "x"),
            GatewayCaido(), contextoLogin, BizCenter, CookiesBizCenter, default);
        var destinoLogin = await Ejecutar(login, contextoLogin);

        destinoRecuperacion.Should().EndWith($"error={AuthApiGateway.Unreachable}");
        destinoLogin.Should().EndWith($"error={AuthApiGateway.Unreachable}");

        contextoRecuperacion.Response.StatusCode.Should().Be(302);
        contextoLogin.Response.StatusCode.Should().Be(302);
    }

    // ===========================================================================================
    //  2. El enrolamiento forzado, que el centro de negocios no sabía manejar
    // ===========================================================================================

    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Login_CuandoLaApiExigeEnrolamiento_VaAlEnrolamientoYNoAErrorInvalid(string nombre)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto();

        var gateway = GatewayQueResponde("""
            {"success":true,"data":{
                "accessToken":"","requiresEnrollment":true,"enrollmentToken":"el-token-de-alta"}}
            """);

        var resultado = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            gateway, contexto, portal, cookies, default);

        var destino = await Ejecutar(resultado, contexto);

        destino.Should().Be(portal.EnrollAuthenticatorPage,
            "un rol que exige segundo factor sin configurar no es una credencial mala: mandarlo a " +
            "?error=invalid le dice al usuario que se ha equivocado de contraseña");

        CookiesEscritas(contexto).Should().ContainSingle(c => c.StartsWith($"{cookies.Enrollment}="))
            .Which.Should().Contain("el-token-de-alta")
            .And.Contain("httponly", "el token del alta no puede leerse desde el navegador");
    }

    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Login_ConEnrolamientoPeroSinToken_VuelveAlLoginSinDejarCookie(string nombre)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto();

        var gateway = GatewayQueResponde(
            """{"success":true,"data":{"accessToken":"","requiresEnrollment":true}}""");

        var resultado = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "x"),
            gateway, contexto, portal, cookies, default);

        var destino = await Ejecutar(resultado, contexto);

        destino.Should().Be($"{portal.LoginPage}?error=invalid");
        CookiesEscritas(contexto).Should().NotContain(c => c.StartsWith($"{cookies.Enrollment}="));
    }

    // ===========================================================================================
    //  3. Lo específico de cada portal sigue entrando por parámetro
    // ===========================================================================================

    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Login_ConSegundoFactor_EscribeElRetoConElNombreDeCookieDeSuPortal(string nombre)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto();

        var gateway = GatewayQueResponde("""
            {"success":true,"data":{
                "accessToken":"","requiresTwoFactor":true,"challengeToken":"el-reto",
                "channel":"Email","maskedTarget":"q****@ejemplo.com"}}
            """);

        var resultado = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            gateway, contexto, portal, cookies, default);

        var destino = await Ejecutar(resultado, contexto);

        // El nombre de la cookie sale de ChallengeCookieNames, que es lo que impide escribir el
        // reto con un nombre y buscarlo con otro — y lo que impide que un portal pise el del otro.
        CookiesEscritas(contexto).Should().ContainSingle(c => c.StartsWith($"{cookies.Login}="))
            .Which.Should().Contain("el-reto");

        // Y el destino enmascarado viaja con el nombre de parámetro que lee la pantalla de ESTE
        // portal: `target` en el componente compartido, `email` en la pantalla propia del centro
        // de negocios.
        destino.Should().Be(
            $"{portal.TwoFactorPage}?{portal.TwoFactorTargetQueryParam}=q%2A%2A%2A%2A%40ejemplo.com");
    }

    [Fact]
    public async Task Login_EnAdministracion_RechazaAQuienNoTieneUnRolDeLaLista()
    {
        var contexto = Contexto();
        var gateway  = GatewayConToken(TokenCon("Member"));

        var resultado = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            gateway, contexto, Admin, CookiesAdmin, default);

        var destino = await Ejecutar(resultado, contexto);

        destino.Should().Be("/admin/login?error=access_denied");
        FirmasDeSesion(contexto).Should().BeEmpty("no se firma sesión para quien no puede entrar");
    }

    [Fact]
    public async Task Login_EnAdministracion_AdmiteAQuienSiTieneUnRolDeLaLista()
    {
        var contexto = Contexto();
        var gateway  = GatewayConToken(TokenCon("SupportLevel2"));

        var resultado = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            gateway, contexto, Admin, CookiesAdmin, default);

        var destino = await Ejecutar(resultado, contexto);

        destino.Should().Be("/admin");
        FirmasDeSesion(contexto).Should().ContainSingle()
            .Which.FindFirst("access_token").Should().NotBeNull(
                "el token de la API viaja en la cookie de sesión: de ahí lo saca AuthApiGateway");
    }

    [Fact]
    public async Task Login_EnElCentroDeNegocios_AdmiteAUnMiembroSinListaDeRoles()
    {
        var contexto = Contexto();
        var gateway  = GatewayConToken(TokenCon("Member"));

        var resultado = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            gateway, contexto, BizCenter, CookiesBizCenter, default);

        var destino = await Ejecutar(resultado, contexto);

        destino.Should().Be("/");
        FirmasDeSesion(contexto).Should().ContainSingle();
    }

    /// <summary>
    /// El idioma del miembro: lo sigue el centro de negocios y no administración, que es lo que
    /// hacía cada uno antes de unificar.
    /// </summary>
    [Theory]
    [InlineData("bizcenter", true)]
    [InlineData("admin",     false)]
    public async Task Login_FijaLaCookieDeCulturaSoloDondeElPortalLoPide(
        string nombre, bool deberiaFijarla)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto();

        var rol     = nombre == "admin" ? "SuperAdmin" : "Member";
        var gateway = GatewayConToken(TokenCon(rol, idiomaPreferido: "es"));

        var resultado = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            gateway, contexto, portal, cookies, default);

        await Ejecutar(resultado, contexto);

        CookiesEscritas(contexto)
            .Any(c => c.StartsWith(".AspNetCore.Culture=", StringComparison.OrdinalIgnoreCase))
            .Should().Be(deberiaFijarla);
    }

    /// <summary>
    /// Un reto gastado no se puede reintentar: fuera la cookie y de vuelta al login. Los dos
    /// portales acababan ahí antes de unificar —administración dando un rebote de más por su
    /// pantalla, que al no encontrar la cookie mandaba al mismo sitio—, así que ahora se va directo.
    /// </summary>
    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task SegundoFactor_ConElRetoGastado_BorraLaCookieYVuelveAlLogin(string nombre)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto(conCookie: (cookies.Login, "un-reto"));

        var gateway = GatewayQueResponde(
            """{"success":false,"errorCode":"CODE_EXPIRED"}""", HttpStatusCode.Unauthorized);

        var resultado = await AuthEndpoints.LoginTwoFactorAsync(
            new AuthEndpoints.CodeForm("123456"),
            gateway, contexto, portal, cookies, default);

        var destino = await Ejecutar(resultado, contexto);

        destino.Should().Be($"{portal.LoginPage}?error=session_expired");
        CookiesEscritas(contexto).Should().Contain(c =>
            c.StartsWith($"{cookies.Login}=") && c.Contains("expires=Thu, 01 Jan 1970"));
    }

    /// <summary>
    /// Sin cookie no hay nada que canjear. Se limpian las dos —el reto del login y el del
    /// enrolamiento— para no dejar atrás la que no provocó la vuelta.
    /// </summary>
    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task SegundoFactor_SinCookieDeReto_VuelveAlLoginSinLlamarALaApi(string nombre)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto();

        // Un gateway que reventaría si alguien lo llamara: si esta prueba pasa, es que no se llamó.
        var resultado = await AuthEndpoints.LoginTwoFactorAsync(
            new AuthEndpoints.CodeForm("123456"),
            GatewayCaido(), contexto, portal, cookies, default);

        var destino = await Ejecutar(resultado, contexto);

        destino.Should().Be($"{portal.LoginPage}?error=session_expired");
    }

    [Theory]
    [MemberData(nameof(LosDosPortales))]
    public async Task Salida_BorraLosDosRetosYCierraLaSesion(string nombre)
    {
        var (portal, cookies) = Portal(nombre);
        var contexto = Contexto();

        var resultado = await AuthEndpoints.LogoutAsync(contexto, portal, cookies);
        var destino   = await Ejecutar(resultado, contexto);

        destino.Should().Be(portal.LoginPage);

        var escritas = CookiesEscritas(contexto);
        escritas.Should().Contain(c => c.StartsWith($"{cookies.Login}="));
        escritas.Should().Contain(c => c.StartsWith($"{cookies.Enrollment}="));
    }

    // ===========================================================================================
    //  Ayudas
    // ===========================================================================================

    /// <summary>Un gateway cuyo cliente HTTP lanza: es SignupAPI apagada, o la red caída.</summary>
    private static AuthApiGateway GatewayCaido() =>
        Gateway(new HandlerFalso(_ => throw new HttpRequestException("SignupAPI no responde")));

    private static AuthApiGateway GatewayQueResponde(
        string cuerpoJson, HttpStatusCode estado = HttpStatusCode.OK) =>
        Gateway(new HandlerFalso(_ => new HttpResponseMessage(estado)
        {
            Content = new StringContent(cuerpoJson, Encoding.UTF8, "application/json")
        }));

    /// <summary>Login correcto: la API devuelve tokens de verdad.</summary>
    private static AuthApiGateway GatewayConToken(string accessToken) =>
        GatewayQueResponde(
            "{\"success\":true,\"data\":{\"accessToken\":\"" + accessToken + "\"}}");

    private static AuthApiGateway Gateway(HttpMessageHandler handler) =>
        new(new FabricaDeClientes(handler),
            new SinToken(),
            NullLogger<AuthApiGateway>.Instance);

    /// <summary>
    /// Un HttpContext con lo justo para que un <see cref="IResult"/> pueda ejecutarse: el registro
    /// que pide la redirección y el servicio de autenticación que pide SignInAsync.
    /// </summary>
    private static DefaultHttpContext Contexto((string Nombre, string Valor)? conCookie = null)
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddSingleton<IAuthenticationService>(new AutenticacionFalsa());

        var contexto = new DefaultHttpContext
        {
            RequestServices = servicios.BuildServiceProvider()
        };

        if (conCookie is not null)
            contexto.Request.Headers.Cookie = $"{conCookie.Value.Nombre}={conCookie.Value.Valor}";

        return contexto;
    }

    /// <summary>Ejecuta el resultado sobre el contexto y devuelve la cabecera Location.</summary>
    private static async Task<string?> Ejecutar(IResult resultado, HttpContext contexto)
    {
        await resultado.ExecuteAsync(contexto);
        return contexto.Response.Headers.Location.ToString();
    }

    private static string[] CookiesEscritas(HttpContext contexto) =>
        contexto.Response.Headers.SetCookie.Select(c => c ?? string.Empty).ToArray();

    private static ClaimsPrincipal[] FirmasDeSesion(HttpContext contexto) =>
        ((AutenticacionFalsa)contexto.RequestServices.GetRequiredService<IAuthenticationService>())
            .Firmadas.ToArray();

    /// <summary>
    /// Un JWT sin firmar con los claims que mira el manejador. No se valida en ningún sitio del
    /// camino de entrada —la firma la comprobó la API al emitirlo—, así que basta con que se pueda
    /// leer.
    /// </summary>
    private static string TokenCon(string rol, string? idiomaPreferido = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   "un-usuario"),
            new(JwtRegisteredClaimNames.Email, "quien@ejemplo.com"),
            new(ClaimTypes.Role, rol)
        };

        if (idiomaPreferido is not null)
            claims.Add(new Claim("default_language", idiomaPreferido));

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer:    "pruebas",
            audience:  "pruebas",
            claims:    claims,
            notBefore: DateTime.UtcNow,
            expires:   DateTime.UtcNow.AddMinutes(15)));
    }

    private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class FabricaDeClientes(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://signupapi.pruebas/")
            };
    }

    /// <summary>El login es anónimo: no hay sesión de la que sacar un token.</summary>
    private sealed class SinToken : IAccessTokenProvider
    {
        public ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<string?>(null);
    }

    /// <summary>
    /// Apunta las llamadas a SignInAsync en vez de montar el esquema de cookie entero: lo que
    /// interesa comprobar es a quién se firma, no cómo se serializa la cookie de sesión.
    /// </summary>
    private sealed class AutenticacionFalsa : IAuthenticationService
    {
        public List<ClaimsPrincipal> Firmadas { get; } = [];

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
            AuthenticationProperties? properties)
        {
            Firmadas.Add(principal);
            return Task.CompletedTask;
        }

        public Task SignOutAsync(
            HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;
    }
}
