using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.ClientCore;
using MLMConquerorGlobalEdition.SharedComponents.Components.Account;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// Los dos manejadores que el panel del segundo factor necesitaba y no existían: cambiar el canal
/// preferido y apagar el segundo factor de una cuenta que ya tiene sesión.
///
/// POR QUÉ ESTABAN SIN ESCRIBIR. El comentario de la pantalla de seguridad decía que SignupAPI no
/// exponía ninguna ruta para estas dos cosas, así que el componente se montaba sin
/// ChangeChannelAction ni DisableAction y —está escrito para que una ruta ausente signifique "esta
/// acción no se ofrece"— no pintaba los botones. Era cierto cuando se escribió y dejó de serlo en
/// af73db0: POST /api/v1/auth/two-factor/channel y /two-factor/disable existen desde entonces. El
/// comentario se quedó, y con él un panel de solo lectura en los dos portales.
///
/// LO QUE VIGILAN ESTAS PRUEBAS es lo mismo que el resto del área: que el destino de la vuelta
/// salga de <see cref="AccountPageRoutes"/> y no escrito a fuego —si no, el miembro del centro de
/// negocios acabaría en una URL de administración—, y que un servicio caído sea una redirección con
/// un mensaje y no un 500 en la cara del usuario.
///
/// Las ayudas de aquí son propias y no las de <c>AuthEndpointsTests</c>: aquellas montan además un
/// servicio de autenticación falso para observar a quién se firma, y estos dos manejadores no
/// firman ninguna sesión — quien llega ya tiene la suya.
/// </summary>
public class AccountTwoFactorEndpointsTests
{
    /// <summary>
    /// Las rutas de los dos portales, con lo único que cambia entre ellos: dónde vive la pantalla.
    /// </summary>
    private static AccountPageRoutes Rutas(string paginaDeSeguridad) => new()
    {
        ForgotPasswordPage     = "/forgot-password",
        ForgotPasswordSentPage = "/forgot-password/sent",
        ResetPasswordPage      = "/reset-password",
        ResetPasswordDonePage  = "/reset-password/done",
        ProfilePage            = "/account",
        PasswordPage           = "/account/password",
        PhonePage              = "/account/phone",
        PhoneVerifyPage        = "/account/phone/confirm",
        SecurityPage           = paginaDeSeguridad,
        PersonalDataPage       = "/account/personal-data"
    };

    /// <summary>Las dos páginas de seguridad que existen hoy, una por portal.</summary>
    public static TheoryData<string> LasDosPantallas() =>
        new() { "/account/security", "/admin/account/security" };

    // ===========================================================================================
    //  Canal preferido
    // ===========================================================================================

    [Theory]
    [MemberData(nameof(LasDosPantallas))]
    public async Task Canal_CuandoSeGuarda_VuelveALaPantallaDeSuPortal(string pantalla)
    {
        var contexto = Contexto();

        var resultado = await AccountEndpoints.SetTwoFactorChannelAsync(
            new AccountEndpoints.TwoFactorChannelForm("Sms"),
            GatewayQueResponde("""{"success":true,"data":true}"""),
            Rutas(pantalla), default);

        var destino = await Ejecutar(resultado, contexto);

        destino.Should().Be(pantalla,
            "el destino sale de AccountPageRoutes: escrito a fuego mandaría al miembro del centro " +
            "de negocios a una URL de administración");
    }

    /// <summary>
    /// El canal se manda TAL CUAL al cuerpo de la llamada. El nombre del campo coincide con el
    /// <c>name=</c> de los radios de TwoFactorPanel; si uno de los dos se renombra, el POST llega
    /// vacío y el miembro se lleva un CHANNEL_UNAVAILABLE sin haber elegido nada mal.
    /// </summary>
    [Fact]
    public async Task Canal_LlegaALaApiConElNombreQuePintaElFormulario()
    {
        string? cuerpo   = null;
        var     contexto = Contexto();

        var gateway = Gateway(new HandlerFalso(peticion =>
        {
            cuerpo = peticion.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"success":true,"data":true}""", Encoding.UTF8, "application/json")
            };
        }));

        var resultado = await AccountEndpoints.SetTwoFactorChannelAsync(
            new AccountEndpoints.TwoFactorChannelForm("Authenticator"),
            gateway, Rutas("/account/security"), default);

        await Ejecutar(resultado, contexto);

        // Sin fijar el caso del nombre: lo decide el serializador, y lo que esta prueba vigila es
        // que el VALOR elegido llegue entero y bajo la clave del canal, no cómo se escribe.
        cuerpo.Should().NotBeNull();
        cuerpo!.ToLowerInvariant().Should().Contain("\"channel\"");
        cuerpo.Should().Contain("Authenticator");
    }

    /// <summary>
    /// El canal sin destino lo rechaza el SERVIDOR, no el portal: la pantalla ya filtra por
    /// AvailableChannels, pero esconder una opción no cierra la ruta. El código se propaga tal cual
    /// para que la pantalla pueda decir qué pasó.
    /// </summary>
    [Fact]
    public async Task Canal_CuandoLaApiLoRechaza_VuelveConSuCodigo()
    {
        var contexto = Contexto();

        var resultado = await AccountEndpoints.SetTwoFactorChannelAsync(
            new AccountEndpoints.TwoFactorChannelForm("Sms"),
            GatewayQueResponde(
                """{"success":false,"errorCode":"CHANNEL_UNAVAILABLE"}""", HttpStatusCode.BadRequest),
            Rutas("/account/security"), default);

        var destino = await Ejecutar(resultado, contexto);

        destino.Should().Be("/account/security?error=CHANNEL_UNAVAILABLE");
    }

    /// <summary>
    /// Un POST sin ningún campo deja el formulario en null. Es lo mismo que cubren los otros nueve
    /// manejadores del área: sin el <c>form ??= new()</c>, esto salía como 500.
    /// </summary>
    [Fact]
    public async Task Canal_ConElFormularioVacio_RedirigeEnVezDeReventar()
    {
        var contexto = Contexto();

        var resultado = await AccountEndpoints.SetTwoFactorChannelAsync(
            form: null,
            GatewayQueResponde(
                """{"success":false,"errorCode":"CHANNEL_UNAVAILABLE"}""", HttpStatusCode.BadRequest),
            Rutas("/account/security"), default);

        await Ejecutar(resultado, contexto);

        contexto.Response.StatusCode.Should().Be(302);
    }

    // ===========================================================================================
    //  Apagar el segundo factor
    // ===========================================================================================

    [Theory]
    [MemberData(nameof(LasDosPantallas))]
    public async Task Baja_CuandoSeApaga_VuelveALaPantallaDeSuPortal(string pantalla)
    {
        var contexto = Contexto();

        var resultado = await AccountEndpoints.DisableTwoFactorAsync(
            GatewayQueResponde("""{"success":true,"data":true}"""),
            Rutas(pantalla), default);

        var destino = await Ejecutar(resultado, contexto);

        destino.Should().Be(pantalla);
    }

    /// <summary>
    /// El rol que exige segundo factor no puede apagarlo, y quien lo decide es el servidor. La
    /// pantalla ya esconde el botón, pero esconder un botón no cierra una ruta: quien la llame a
    /// mano se lleva TWO_FACTOR_REQUIRED, y ese código tiene texto propio en la interfaz.
    /// </summary>
    [Fact]
    public async Task Baja_CuandoElRolLaExige_VuelveConTwoFactorRequired()
    {
        var contexto = Contexto();

        var resultado = await AccountEndpoints.DisableTwoFactorAsync(
            GatewayQueResponde(
                """{"success":false,"errorCode":"TWO_FACTOR_REQUIRED"}""", HttpStatusCode.BadRequest),
            Rutas("/admin/account/security"), default);

        var destino = await Ejecutar(resultado, contexto);

        destino.Should().Be("/admin/account/security?error=TWO_FACTOR_REQUIRED");

        AccountMessages.ErrorKeyFor("TWO_FACTOR_REQUIRED")
            .Should().NotBe("Account.Error.Generic",
                "un código que el manejador propaga y la pantalla no sabe traducir sale como " +
                "\"algo salió mal\", que no dice nada de una política de la organización");
    }

    // ===========================================================================================
    //  Con SignupAPI caída, ninguno de los dos revienta
    // ===========================================================================================

    [Fact]
    public async Task NingunoDeLosDosDejaEscaparLaExcepcionDeUnServicioCaido()
    {
        var rutas = Rutas("/account/security");

        var manejadores = new Func<Task<IResult>>[]
        {
            () => AccountEndpoints.SetTwoFactorChannelAsync(
                new AccountEndpoints.TwoFactorChannelForm("Email"), GatewayCaido(), rutas, default),

            () => AccountEndpoints.DisableTwoFactorAsync(GatewayCaido(), rutas, default),
        };

        foreach (var manejador in manejadores)
        {
            var contexto  = Contexto();
            var resultado = await manejador();
            var destino   = await Ejecutar(resultado, contexto);

            contexto.Response.StatusCode.Should().Be(302);
            destino.Should().Be($"/account/security?error={AuthApiGateway.Unreachable}");
        }
    }

    // ===========================================================================================
    //  Ayudas
    // ===========================================================================================

    private static AuthApiGateway GatewayCaido() =>
        Gateway(new HandlerFalso(_ => throw new HttpRequestException("SignupAPI no responde")));

    private static AuthApiGateway GatewayQueResponde(
        string cuerpoJson, HttpStatusCode estado = HttpStatusCode.OK) =>
        Gateway(new HandlerFalso(_ => new HttpResponseMessage(estado)
        {
            Content = new StringContent(cuerpoJson, Encoding.UTF8, "application/json")
        }));

    private static AuthApiGateway Gateway(HttpMessageHandler handler) =>
        new(new FabricaDeClientes(handler),
            new ConToken(),
            NullLogger<AuthApiGateway>.Instance);

    private static DefaultHttpContext Contexto()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();

        return new DefaultHttpContext { RequestServices = servicios.BuildServiceProvider() };
    }

    private static async Task<string?> Ejecutar(IResult resultado, HttpContext contexto)
    {
        await resultado.ExecuteAsync(contexto);
        return contexto.Response.Headers.Location.ToString();
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

    /// <summary>
    /// Estos dos manejadores llaman con Bearer, así que hace falta un token: sin él el gateway
    /// devolvería SESSION_EXPIRED antes de llegar a la red y las pruebas medirían otra cosa.
    /// </summary>
    private sealed class ConToken : IAccessTokenProvider
    {
        public ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default) =>
            ValueTask.FromResult<string?>("un-token-de-sesion");
    }
}
