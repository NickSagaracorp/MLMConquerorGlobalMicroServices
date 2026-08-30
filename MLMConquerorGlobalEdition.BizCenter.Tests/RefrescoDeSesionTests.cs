using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.ClientCore;
using MLMConquerorGlobalEdition.SharedComponents.Server.Extensions;
using MLMConquerorGlobalEdition.SharedComponents.Server.Services;

namespace MLMConquerorGlobalEdition.BizCenter.Tests;

/// <summary>
/// La sesión se renueva sola mientras el usuario esté activo, y solo muere cuando de verdad no se
/// puede renovar.
///
/// ESTE ARCHIVO EXISTE PORQUE EL FALLO ANTERIOR ERA UNA OMISIÓN, NO UN ERROR. La puerta recibía el
/// refresh token en cada inicio de sesión y lo tiraba; sin él, caducar el JWT ERA que la sesión
/// estaba muerta, y a las dos horas el usuario volvía al login aunque acabara de pulsar un botón.
/// Nada fallaba, nada se ponía rojo: simplemente no estaba escrito.
///
/// Y es el tipo de cosa que vuelve a no estar escrita sin que se note, porque todo lo que aquí se
/// prueba solo se ve cuando algo CADUCA. Los cinco grupos, y qué guarda cada uno:
///
///   1. La captura — que el refresh token llegue del <c>Set-Cookie</c> a la cookie de sesión. Si
///      esto se rompe, todo lo demás sigue compilando y la sesión vuelve a morir a las dos horas.
///   2. La rotación encadenada — la API entrega un refresh NUEVO en cada renovación. Con un solo
///      refresco todo parece funcionar; el fallo aparece en el segundo.
///   3. La carrera — varias llamadas descubriendo la caducidad a la vez no pueden gastar el mismo
///      token rotatorio, o la primera salva la sesión y las demás la matan.
///   4. Los dos caminos del portal — el circuito, donde la cookie NO se puede reescribir, y el
///      middleware, que es quien la pone al día.
///   5. Lo que sigue muriendo — una sesión sin salvación acaba en el login, y salir de verdad mata
///      el refresh token en la API.
/// </summary>
public class RefrescoDeSesionTests
{
    private const string LoginBizCenter  = "/login";
    private const string SalidaDelPortal = "/account/logout";

    private static readonly ChallengeCookieNames Cookies = new()
    {
        Login      = "mlm_pruebas_2fa_challenge",
        Enrollment = "mlm_pruebas_2fa_enrollment",
        Phone      = "mlm_pruebas_phone_challenge"
    };

    private static readonly AuthPortalOptions BizCenter = new()
    {
        LoginPage               = LoginBizCenter,
        TwoFactorPage           = "/two-factor",
        EnrollAuthenticatorPage = "/enroll-authenticator",
        HomePage                = "/"
    };

    // ===============================================================================================
    //  1. La captura: el refresh token tiene que llegar del Set-Cookie a la cookie de sesión
    // ===============================================================================================

    /// <summary>
    /// EL FALLO ORIGINAL, EN UNA PRUEBA. La API entrega el refresh token en la cabecera
    /// <c>Set-Cookie</c> —el cuerpo lo trae vacío a propósito— y la puerta lo descartaba. Aquí se
    /// exige que acabe donde tiene que acabar: como claim de la cookie de sesión, al lado del de
    /// acceso.
    /// </summary>
    [Fact]
    public async Task Login_GuardaElRefreshTokenQueLaApiDejaEnLaCabecera()
    {
        var contexto = Contexto();
        var almacen  = Almacen(new ApiFalsa());

        var resultado = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            GatewayQueEntregaTokens(TokenVivo(), "el-refresco-1"),
            contexto, BizCenter, Cookies, almacen, default);

        await resultado.ExecuteAsync(contexto);

        var firmado = Firmadas(contexto).Should().ContainSingle().Subject;

        firmado.FindFirst(SessionExpiry.RefreshTokenClaim)?.Value.Should().Be("el-refresco-1",
            "sin esto no hay con qué renovar, y caducar el JWT vuelve a ser el final de la sesión");

        firmado.FindFirst(SessionExpiry.AccessTokenClaim).Should().NotBeNull(
            "el token de acceso sigue donde estaba: esto añade, no sustituye");

        firmado.FindFirst(SessionExpiry.SessionIdClaim)?.Value.Should().NotBeNullOrWhiteSpace(
            "el circuito y la petición siguiente son dos mundos distintos y necesitan poder " +
            "señalar la misma pareja de tokens");
    }

    /// <summary>
    /// Y además queda sembrado el almacén, que es lo que ven el circuito y el middleware. Sin esto
    /// la primera renovación saldría de la cookie —que también vale— pero la sesión no tendría
    /// entrada, y con ella se va el semáforo que impide la carrera.
    /// </summary>
    [Fact]
    public async Task Login_DejaSembradoElAlmacenDeSesion()
    {
        var contexto = Contexto();
        var almacen  = Almacen(new ApiFalsa());

        var resultado = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            GatewayQueEntregaTokens(TokenVivo(), "el-refresco-1"),
            contexto, BizCenter, Cookies, almacen, default);
        await resultado.ExecuteAsync(contexto);

        var firmado = Firmadas(contexto).Single();

        almacen.Count.Should().Be(1);
        almacen.Current(firmado)!.RefreshToken.Should().Be("el-refresco-1");
    }

    /// <summary>
    /// Una API que no entregue refresh token no puede tumbar el login: la sesión se firma igual y
    /// simplemente no se podrá renovar, que es exactamente lo que pasaba antes de todo esto.
    /// </summary>
    [Fact]
    public async Task Login_SinRefreshTokenEnLaRespuesta_FirmaLaSesionIgual()
    {
        var contexto = Contexto();

        var resultado = await AuthEndpoints.LoginAsync(
            new AuthEndpoints.LoginForm("quien@ejemplo.com", "la-contraseña"),
            GatewayQueEntregaTokens(TokenVivo(), refreshToken: null),
            contexto, BizCenter, Cookies, Almacen(new ApiFalsa()), default);
        await resultado.ExecuteAsync(contexto);

        var firmado = Firmadas(contexto).Should().ContainSingle().Subject;
        firmado.FindFirst(SessionExpiry.AccessTokenClaim).Should().NotBeNull();
        firmado.FindFirst(SessionExpiry.RefreshTokenClaim).Should().BeNull();
    }

    /// <summary>
    /// La lectura de la cabecera, por partes. Una respuesta puede traer varias cookies y cada una
    /// con sus atributos detrás; quedarse con el nombre equivocado o arrastrar el <c>; Path=/</c>
    /// produce un token que la API rechaza, y eso se vería solo al primer refresco.
    /// </summary>
    [Fact]
    public void LaCookieDelRefresco_SeLeeEntreLasDemasYSinSusAtributos()
    {
        var respuesta = new HttpResponseMessage(HttpStatusCode.OK);
        respuesta.Headers.Add("Set-Cookie", "otra_cosa=xxx; Path=/");
        respuesta.Headers.Add("Set-Cookie",
            "refresh_token=el-valor-bueno; Path=/; HttpOnly; Secure; SameSite=Strict");

        RefreshCookie.ReadFrom(respuesta).Should().Be("el-valor-bueno");
    }

    [Fact]
    public void SinCookieDeRefresco_NoSeInventaNinguna()
    {
        var respuesta = new HttpResponseMessage(HttpStatusCode.OK);
        respuesta.Headers.Add("Set-Cookie", "otra_cosa=xxx; Path=/");

        RefreshCookie.ReadFrom(respuesta).Should().BeNull();
        RefreshCookie.ReadFrom(new HttpResponseMessage(HttpStatusCode.OK)).Should().BeNull();
    }

    // ===============================================================================================
    //  2. La rotación encadenada
    // ===============================================================================================

    /// <summary>
    /// DOS REFRESCOS SEGUIDOS, que es donde aparece el fallo que no se ve con uno.
    ///
    /// La API ROTA el refresh token: el que entra queda gastado y el que sale es otro. Si el portal
    /// se queda con el viejo, el primer refresco funciona —y todo parece bien— y el segundo devuelve
    /// INVALID_REFRESH_TOKEN, tirando al usuario al login a mitad de sesión. La API falsa de aquí
    /// rota igual que la de verdad y rechaza cualquier token que ya haya gastado.
    /// </summary>
    [Fact]
    public async Task DosRefrescosSeguidos_CadaUnoUsaElTokenQueDevolvioElAnterior()
    {
        // Cada renovación devuelve un token de acceso YA caducado: es un usuario que vuelve mucho
        // más tarde cada vez, sin que la prueba tenga que esperar dos horas.
        var api     = new ApiFalsa { VidaDelToken = TimeSpan.FromMinutes(-1) };
        var almacen = Almacen(api);
        var usuario = Usuario(TokenCaducado(), "refresco-0", "sesion-1");

        var primero = await almacen.EnsureFreshAsync(usuario);
        primero.Should().NotBeNull();
        primero!.RefreshToken.Should().Be("refresco-1", "la API entrega uno nuevo en cada renovación");

        // Segundo vencimiento, y con él el segundo refresco.
        var segundo = await almacen.EnsureFreshAsync(usuario);
        segundo.Should().NotBeNull(
            "si el portal hubiera vuelto a mandar refresco-0, la API lo habría rechazado y esta " +
            "sesión estaría muerta a mitad de camino");
        segundo!.RefreshToken.Should().Be("refresco-2");

        api.TokensRecibidos.Should().Equal(["refresco-0", "refresco-1"],
            "cada renovación tiene que ir con el token que devolvió la anterior, nunca con el de la " +
            "cookie, que se quedó en el primero");
        api.Renovaciones.Should().Be(2);
    }

    /// <summary>
    /// Tres seguidos, por si dos fueran casualidad: la cadena tiene que aguantar tanto como dure la
    /// sesión.
    /// </summary>
    [Fact]
    public async Task TresRefrescosSeguidos_LaCadenaNoSeRompe()
    {
        var api     = new ApiFalsa { VidaDelToken = TimeSpan.FromMinutes(-1) };
        var almacen = Almacen(api);
        var usuario = Usuario(TokenCaducado(), "refresco-0", "sesion-1");

        for (var vuelta = 0; vuelta < 3; vuelta++)
            (await almacen.EnsureFreshAsync(usuario)).Should().NotBeNull($"vuelta {vuelta}");

        api.Renovaciones.Should().Be(3);
        api.TokensRechazados.Should().Be(0, "ninguna renovación reutilizó un token ya gastado");
    }

    /// <summary>
    /// Con el token todavía vivo no se renueva nada. Parece obvio y no lo es: renovar en cada
    /// llamada gastaría un viaje a SignupAPI por clic y, con la rotación, convertiría cualquier
    /// solapamiento en una carrera.
    /// </summary>
    [Fact]
    public async Task ConElTokenVivo_NoSeLlamaALaApi()
    {
        var api     = new ApiFalsa();
        var almacen = Almacen(api);

        var vigentes = await almacen.EnsureFreshAsync(Usuario(TokenVivo(), "refresco-0", "sesion-1"));

        vigentes.Should().NotBeNull();
        api.Renovaciones.Should().Be(0);
    }

    // ===============================================================================================
    //  3. La carrera
    // ===============================================================================================

    /// <summary>
    /// OCHO LLAMADAS DESCUBRIENDO LA CADUCIDAD A LA VEZ. No es un caso raro: una pantalla con tres
    /// grids lanza tres lecturas simultáneas, y las tres ven el mismo token caducado en el mismo
    /// instante.
    ///
    /// Con la rotación, ocho renovaciones con el mismo token significan UNA que funciona y SIETE que
    /// reciben INVALID_REFRESH_TOKEN — y esas siete matan la sesión que la primera acababa de
    /// salvar. Tiene que salir UNA sola llamada a la API y las ocho tienen que llevarse la misma
    /// pareja.
    /// </summary>
    [Fact]
    public async Task OchoLlamadasSimultaneas_ProducenUnaSolaRenovacion()
    {
        // Con retardo: sin él la primera podría terminar antes de que las demás lleguen, y la
        // prueba pasaría sin haber ejercido nunca la concurrencia.
        var api     = new ApiFalsa { Retardo = TimeSpan.FromMilliseconds(60) };
        var almacen = Almacen(api);
        var usuario = Usuario(TokenCaducado(), "refresco-0", "sesion-1");

        var enParalelo = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(() => almacen.EnsureFreshAsync(usuario))));

        api.Renovaciones.Should().Be(1,
            "el primero renueva y los demás esperan y se llevan lo suyo: gastar el token rotatorio " +
            "ocho veces es matar la sesión siete");
        api.TokensRechazados.Should().Be(0);

        enParalelo.Should().AllSatisfy(r => r.Should().NotBeNull());
        enParalelo.Select(r => r!.AccessToken).Distinct().Should().ContainSingle(
            "las ocho llamadas son la misma sesión y tienen que seguir con el mismo token");
    }

    /// <summary>
    /// Dos usuarios distintos renovando a la vez NO se estorban. El semáforo es por sesión y no
    /// global a propósito: con ciento diecinueve mil cuentas, un semáforo único convertiría cada
    /// vencimiento en una cola.
    /// </summary>
    [Fact]
    public async Task DosSesionesDistintas_RenuevanALaVezSinEsperarse()
    {
        // Dos refrescos iniciales: son dos cuentas, y en la API de verdad cada una tiene el suyo.
        // La API no responde a ninguna hasta que las DOS estén dentro, así que si el portal las
        // hubiera puesto en fila la primera se quedaría esperando a una segunda que no puede llegar.
        var api     = new ApiFalsa("refresco-a", "refresco-b") { JuntarAntesDeResponder = 2 };
        var almacen = Almacen(api);

        var uno = Usuario(TokenCaducado(), "refresco-a", "sesion-1");
        var dos = Usuario(TokenCaducado(), "refresco-b", "sesion-2");

        var enParalelo = await Task.WhenAll(
            Task.Run(() => almacen.EnsureFreshAsync(uno)),
            Task.Run(() => almacen.EnsureFreshAsync(dos)));

        api.Renovaciones.Should().Be(2, "son dos sesiones: cada una renueva la suya");
        api.SeSerializaron.Should().Be(0,
            "el semáforo es POR SESIÓN: con uno global, un vencimiento pondría en fila a los " +
            "ciento diecinueve mil");
        enParalelo.Should().AllSatisfy(r => r.Should().NotBeNull());
    }

    // ===============================================================================================
    //  4. Los dos caminos del portal
    // ===============================================================================================

    /// <summary>
    /// DENTRO DEL CIRCUITO, que es el caso del usuario que pulsa un botón después de comer.
    ///
    /// La llamada tiene que SALIR, con el Bearer nuevo, y nadie tiene que navegar a ninguna parte.
    /// Antes esto era una navegación forzada a la salida del portal: la acción se perdía y el
    /// usuario aparecía en el login.
    /// </summary>
    [Fact]
    public async Task EnElCircuito_ConElTokenCaducado_RenuevaYLaLlamadaSale()
    {
        var api    = new ApiFalsa();
        var mundo  = new MundoDelCircuito(api);

        var respuesta = await mundo.LlamarAsync(TokenCaducado(), "refresco-0", "sesion-1");

        respuesta.StatusCode.Should().Be(HttpStatusCode.OK,
            "la acción del usuario tiene que funcionar, no acabar en el login");

        mundo.AutorizacionRecibida.Should().Be($"Bearer {api.TokenDeAccesoVigente}",
            "y tiene que salir con el token NUEVO, no con el caducado");

        mundo.Navegaciones.Should().BeEmpty(
            "renovar es lo contrario de echar al usuario: nadie navega a ninguna parte");
    }

    /// <summary>
    /// La renovación de dentro del circuito queda en el almacén. La cookie no se puede reescribir
    /// desde ahí —la respuesta a mano es la del WebSocket y ya empezó—, así que el almacén es la
    /// única forma de que la siguiente petición se entere de la rotación.
    /// </summary>
    [Fact]
    public async Task EnElCircuito_LaRotacionQuedaEnElAlmacenAunqueLaCookieSigaConLaVieja()
    {
        var api   = new ApiFalsa();
        var mundo = new MundoDelCircuito(api);

        await mundo.LlamarAsync(TokenCaducado(), "refresco-0", "sesion-1");

        var usuario = Usuario(TokenCaducado(), "refresco-0", "sesion-1");
        mundo.Almacen.Current(usuario)!.RefreshToken.Should().Be("refresco-1",
            "la cookie sigue con refresco-0 y el almacén ya va por refresco-1: si la siguiente " +
            "navegación mirara la cookie, renovaría con un token que la API ya invalidó");
    }

    /// <summary>
    /// EL MIDDLEWARE, que es el camino de la recarga: F5 con el token ya caducado. El circuito
    /// todavía no existe, así que aquí no hay quien renueve salvo esto.
    ///
    /// Y como aquí SÍ hay una respuesta que no ha empezado, además de renovar reemite la cookie.
    /// </summary>
    [Fact]
    public async Task ElMiddleware_ConElTokenCaducado_RenuevaReemiteLaCookieYDejaPasar()
    {
        var api      = new ApiFalsa();
        var almacen  = Almacen(api);
        var contexto = Navegacion("/una/pantalla", TokenCaducado(), "refresco-0", "sesion-1");

        var siguio = await Ejecutar(contexto, BizCenter, almacen);

        siguio.Should().BeTrue("la navegación tiene que llegar a su pantalla, no al login");
        Autenticacion(contexto).Salidas.Should().Be(0);

        var reemitida = Firmadas(contexto).Should().ContainSingle().Subject;
        reemitida.FindFirst(SessionExpiry.AccessTokenClaim)?.Value
            .Should().Be(api.TokenDeAccesoVigente);
        reemitida.FindFirst(SessionExpiry.RefreshTokenClaim)?.Value.Should().Be("refresco-1");

        contexto.User.FindFirst(SessionExpiry.AccessTokenClaim)?.Value
            .Should().Be(api.TokenDeAccesoVigente,
                "el apretón de manos del circuito se lleva el principal de ESTA petición: sin " +
                "actualizarlo, el circuito arrancaría con el token viejo");
    }

    /// <summary>
    /// LA REEMISIÓN QUE CIERRA EL CÍRCULO. El circuito renovó y la cookie se quedó atrás; en la
    /// siguiente navegación el token de la cookie ni siquiera está caducado —el almacén tiene otro
    /// más nuevo— y aun así hay que reescribirla, porque el refresco que lleva dentro ya está
    /// gastado. Si esto no ocurriera, un reinicio del portal dejaría esa sesión sin poder renovarse
    /// nunca más.
    /// </summary>
    [Fact]
    public async Task ElMiddleware_CuandoElAlmacenVaPorDelante_PoneLaCookieAlDia()
    {
        var api     = new ApiFalsa();
        var almacen = Almacen(api);

        // Lo que deja un circuito que ya renovó: el almacén con la pareja nueva…
        var usuario = Usuario(TokenCaducado(), "refresco-0", "sesion-1");
        await almacen.EnsureFreshAsync(usuario);

        // …y la cookie del navegador todavía con la vieja.
        var contexto = Navegacion("/una/pantalla", TokenCaducado(), "refresco-0", "sesion-1");

        var siguio = await Ejecutar(contexto, BizCenter, almacen);

        siguio.Should().BeTrue();
        api.Renovaciones.Should().Be(1, "no hacía falta renovar otra vez: ya estaba hecho");

        Firmadas(contexto).Should().ContainSingle()
            .Which.FindFirst(SessionExpiry.RefreshTokenClaim)?.Value.Should().Be("refresco-1");
    }

    /// <summary>
    /// Con todo al día no se reescribe la cookie. Reemitirla en cada navegación sería escribir una
    /// cabecera <c>Set-Cookie</c> de kilobyte y medio en cada página por nada.
    /// </summary>
    [Fact]
    public async Task ElMiddleware_ConTodoAlDia_NoTocaLaCookie()
    {
        var almacen  = Almacen(new ApiFalsa());
        var contexto = Navegacion("/una/pantalla", TokenVivo(), "refresco-0", "sesion-1");

        var siguio = await Ejecutar(contexto, BizCenter, almacen);

        siguio.Should().BeTrue();
        Firmadas(contexto).Should().BeEmpty();
    }

    /// <summary>
    /// Los POST del área de cuenta también renuevan, y no por el middleware —que solo mira
    /// navegaciones— sino por el proveedor de token, que es por donde pasan todos. Sin esto,
    /// cambiar la contraseña con el JWT caducado devolvía SESSION_EXPIRED aunque el usuario acabara
    /// de teclear la actual.
    /// </summary>
    [Fact]
    public async Task ElProveedorDeToken_RenuevaCuandoElDeLaCookieYaCaduco()
    {
        var api      = new ApiFalsa();
        var almacen  = Almacen(api);
        var contexto = new DefaultHttpContext
        {
            User = Usuario(TokenCaducado(), "refresco-0", "sesion-1")
        };

        var proveedor = new HttpContextAccessTokenProvider(
            new HttpContextAccessor { HttpContext = contexto }, almacen);

        var token = await proveedor.GetAccessTokenAsync();

        token.Should().Be(api.TokenDeAccesoVigente);
        api.Renovaciones.Should().Be(1);
    }

    // ===============================================================================================
    //  5. Lo que sigue muriendo
    // ===============================================================================================

    /// <summary>
    /// EL REQUISITO QUE NO SE NEGOCIA: una sesión muerta de verdad sigue acabando en el login con su
    /// aviso. La renovación es un intento previo, no un sustituto — si el refresh token está
    /// caducado, revocado o no existe, el final es el de siempre.
    /// </summary>
    [Fact]
    public async Task ElMiddleware_ConElRefrescoRevocado_CierraLaSesionYMandaAlLogin()
    {
        var api      = new ApiFalsa("el-bueno");
        var almacen  = Almacen(api);

        // La cookie lleva un refresco que la API no reconoce: es lo que queda después de salir en
        // otra pestaña, o de borrar la columna en la base.
        var contexto = Navegacion("/una/pantalla", TokenCaducado(), "el-revocado", "sesion-1");

        var siguio = await Ejecutar(contexto, BizCenter, almacen);

        siguio.Should().BeFalse("la petición se corta aquí, no sigue al render");
        Autenticacion(contexto).Salidas.Should().Be(1, "la cookie de sesión tiene que limpiarse");
        contexto.Response.Headers.Location.ToString()
            .Should().Be($"{LoginBizCenter}?error=session_expired");
    }

    /// <summary>
    /// Sin refresh token —una cookie firmada antes de que existiera la renovación— tampoco hay nada
    /// que intentar, y no se gasta ni una llamada a la API en averiguarlo.
    /// </summary>
    [Fact]
    public async Task ElMiddleware_SinRefreshToken_MandaAlLoginSinLlamarALaApi()
    {
        var api      = new ApiFalsa();
        var almacen  = Almacen(api);
        var contexto = Navegacion("/una/pantalla", TokenCaducado(), refreshToken: null, sessionId: null);

        var siguio = await Ejecutar(contexto, BizCenter, almacen);

        siguio.Should().BeFalse();
        api.Renovaciones.Should().Be(0);
        contexto.Response.Headers.Location.ToString()
            .Should().Be($"{LoginBizCenter}?error=session_expired");
    }

    /// <summary>
    /// Y con SignupAPI caída, igual: al login con su aviso, no un 500 ni una pantalla colgada.
    /// </summary>
    [Fact]
    public async Task ElMiddleware_ConLaApiCaida_MandaAlLoginConElAviso()
    {
        var almacen  = Almacen(new ApiFalsa { Caida = true });
        var contexto = Navegacion("/una/pantalla", TokenCaducado(), "refresco-0", "sesion-1");

        var siguio = await Ejecutar(contexto, BizCenter, almacen);

        siguio.Should().BeFalse();
        contexto.Response.Headers.Location.ToString()
            .Should().Be($"{LoginBizCenter}?error=session_expired");
    }

    /// <summary>
    /// En el circuito, lo mismo: con el refresco revocado se vuelve a la salida del portal, que es
    /// lo único que puede limpiar la cookie desde ahí. Ese camino es el de 3763f9e y sigue intacto.
    /// </summary>
    [Fact]
    public async Task EnElCircuito_ConElRefrescoRevocado_VuelveALaSalidaDelPortal()
    {
        var mundo = new MundoDelCircuito(new ApiFalsa("el-bueno"));

        var respuesta = await mundo.LlamarAsync(TokenCaducado(), "el-revocado", "sesion-1");

        respuesta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        mundo.Navegaciones.Should().ContainSingle()
            .Which.Should().Be((SessionExpiry.LogoutUrl(SalidaDelPortal), true));
    }

    /// <summary>
    /// Una sesión que no se pudo renovar se OLVIDA en el almacén. Si se quedara, cada clic de la
    /// pantalla que aún está en pantalla mientras vuela la navegación a la salida sería otro viaje a
    /// SignupAPI con el mismo token muerto.
    /// </summary>
    [Fact]
    public async Task LaSesionQueNoSePudoRenovar_SeOlvidaEnElAlmacen()
    {
        var api     = new ApiFalsa("el-bueno");
        var almacen = Almacen(api);
        var usuario = Usuario(TokenCaducado(), "el-revocado", "sesion-1");

        (await almacen.EnsureFreshAsync(usuario)).Should().BeNull();
        almacen.Count.Should().Be(0);
    }

    /// <summary>
    /// SALIR TIENE QUE MATAR EL REFRESH TOKEN EN LA API, no solo la cookie del portal. Desde que el
    /// portal lo guarda, borrar la cookie deja viva una credencial de treinta días que sirve para
    /// pedir tokens de acceso nuevos sin contraseña.
    /// </summary>
    [Fact]
    public async Task Salir_InvalidaElRefreshTokenEnLaApi()
    {
        var api      = new ApiFalsa();
        var almacen  = Almacen(api);
        var vivo     = TokenVivo();
        var contexto = ContextoDe(Usuario(vivo, "refresco-0", "sesion-1"));

        almacen.Seed("sesion-1", new SessionTokens(vivo, "refresco-0"));

        var resultado = await AuthEndpoints.LogoutAsync(
            contexto, GatewayDe(api, vivo), BizCenter, Cookies, almacen);
        await resultado.ExecuteAsync(contexto);

        api.Salidas.Should().Be(1,
            "sin esta llamada, salir solo esconde la sesión: el refresco sigue vivo en la base");
        api.AutorizacionDeLaSalida.Should().Be($"Bearer {vivo}");

        almacen.Count.Should().Be(0, "y la credencial tampoco puede quedarse en memoria");
        contexto.Response.Headers.Location.ToString().Should().Be(LoginBizCenter);
    }

    /// <summary>
    /// Con SignupAPI caída, salir sigue saliendo. Una salida a medias porque un servicio no responde
    /// sería peor que un refresh token que se queda vivo hasta que caduque solo.
    /// </summary>
    [Fact]
    public async Task Salir_ConLaApiCaida_CierraLaSesionDelPortalIgual()
    {
        var api      = new ApiFalsa { Caida = true };
        var almacen  = Almacen(api);
        var vivo     = TokenVivo();
        var contexto = ContextoDe(Usuario(vivo, "refresco-0", "sesion-1"));

        var resultado = await AuthEndpoints.LogoutAsync(
            contexto, GatewayDe(api, vivo), BizCenter, Cookies, almacen);
        await resultado.ExecuteAsync(contexto);

        Autenticacion(contexto).Salidas.Should().Be(1);
        contexto.Response.Headers.Location.ToString().Should().Be(LoginBizCenter);
    }

    // ===============================================================================================
    //  El registro del cliente HTTP
    // ===============================================================================================

    /// <summary>
    /// LAS COOKIES DEL CLIENTE A SIGNUPAPI TIENEN QUE ESTAR APAGADAS, y esto es una fuga entre
    /// usuarios, no una preferencia.
    ///
    /// <c>IHttpClientFactory</c> construye UN manejador primario por cliente con nombre y lo
    /// reutiliza para TODAS las llamadas de TODOS los usuarios. Con <c>UseCookies</c> en <c>true</c>
    /// —que es como viene— ese manejador guarda en un <c>CookieContainer</c> COMPARTIDO el
    /// <c>Set-Cookie</c> con el que la API entrega el refresh token de quien acaba de entrar, y lo
    /// reenvía en la siguiente llamada de cualquier otro. Una sesión hablando con la credencial de
    /// otra, y sin que nada falle a la vista.
    /// </summary>
    [Fact]
    public void ElClienteDeSignupApi_NoGuardaNiReenviaCookies()
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddAuthApiClient("https://signupapi.pruebas");

        using var raiz = servicios.BuildServiceProvider();

        var cadena = raiz.GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(AuthApiGateway.HttpClientName);

        var primario = cadena;
        while (primario is DelegatingHandler intermedio && intermedio.InnerHandler is not null)
            primario = intermedio.InnerHandler;

        primario.Should().BeOfType<HttpClientHandler>()
            .Which.UseCookies.Should().BeFalse(
                "el contenedor de cookies es compartido entre todos los usuarios de este portal: " +
                "el refresh token de uno saldría enganchado en la llamada de otro");
    }

    // ===============================================================================================
    //  Ayudas
    // ===============================================================================================

    /// <summary>
    /// SignupAPI de mentira, con lo que importa de la de verdad: ROTA el refresh token en cada
    /// renovación y RECHAZA cualquiera que ya haya gastado. Sin esas dos cosas, una prueba de
    /// rotación encadenada pasaría aunque el portal reutilizara siempre el mismo token.
    /// </summary>
    private sealed class ApiFalsa : HttpMessageHandler
    {
        /// <summary>
        /// Los refresh tokens que ahora mismo valen. Es un CONJUNTO y no un valor porque hay
        /// pruebas con dos sesiones a la vez, y en la API de verdad cada cuenta tiene el suyo.
        /// Quitar el recibido y meter el nuevo EN UN SOLO PASO atómico es lo que hace que la
        /// rotación se comporte como la real bajo concurrencia: de ocho llamadas con el mismo
        /// token, exactamente una se lo lleva.
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> _vigentes = new(StringComparer.Ordinal);

        private readonly ConcurrentQueue<string> _recibidos = new();

        private readonly TaskCompletionSource _todasDentro =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int    _rotaciones;
        private int    _renovaciones;
        private int    _rechazados;
        private int    _salidas;
        private int    _enVuelo;
        private int    _serializadas;
        private string _ultimoTokenDeAcceso = TokenVivo();

        public ApiFalsa(params string[] refrescosIniciales)
        {
            foreach (var token in refrescosIniciales.Length > 0 ? refrescosIniciales : ["refresco-0"])
                _vigentes[token] = 0;
        }

        /// <summary>Para que la carrera sea de verdad una carrera y no una fila.</summary>
        public TimeSpan Retardo { get; init; } = TimeSpan.Zero;

        /// <summary>
        /// Cuántas renovaciones tienen que estar DENTRO de la API a la vez antes de que ninguna
        /// pueda salir.
        /// </summary>
        /// <remarks>
        /// Es la forma de comprobar que dos sesiones distintas no se estorban sin medir tiempos: si
        /// el portal las serializara, la primera se quedaría esperando a una segunda que no puede
        /// entrar hasta que ella salga, y eso se apunta en <see cref="SeSerializaron"/>. Un reloj
        /// diría lo mismo la mayoría de las veces y se pondría rojo solo en la máquina lenta de
        /// otro.
        /// </remarks>
        public int JuntarAntesDeResponder { get; init; }

        /// <summary>SignupAPI apagada, o la red caída.</summary>
        public bool Caida { get; init; }

        /// <summary>
        /// Cuánto vive el token de acceso que emite la renovación. En negativo emite tokens YA
        /// caducados, que es cómo se encadena un refresco tras otro sin esperar dos horas: equivale
        /// a un usuario que vuelve mucho más tarde cada vez.
        /// </summary>
        public TimeSpan VidaDelToken { get; init; } = TimeSpan.FromMinutes(15);

        public int      Renovaciones           => Volatile.Read(ref _renovaciones);
        public int      SeSerializaron         => Volatile.Read(ref _serializadas);
        public int      TokensRechazados       => Volatile.Read(ref _rechazados);
        public int      Salidas                => Volatile.Read(ref _salidas);
        public string   TokenDeAccesoVigente   => _ultimoTokenDeAcceso;
        public string?  AutorizacionDeLaSalida { get; private set; }
        public string[] TokensRecibidos        => [.. _recibidos];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Caida) throw new HttpRequestException("SignupAPI no responde");

            var ruta = request.RequestUri!.AbsolutePath;

            if (ruta.EndsWith("/logout", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _salidas);
                AutorizacionDeLaSalida = request.Headers.Authorization?.ToString();

                // Como la de verdad: la salida invalida el refresco de esa cuenta. Aquí, todos.
                _vigentes.Clear();
                return Json("""{"success":true,"data":true}""");
            }

            if (!ruta.EndsWith("/refresh", StringComparison.Ordinal))
                return Json("""{"success":true,"data":true}""");

            var recibido = request.Headers.TryGetValues("Cookie", out var cookies)
                ? cookies.FirstOrDefault()?.Replace($"{RefreshCookie.Name}=", string.Empty)
                : null;

            _recibidos.Enqueue(recibido ?? "(ninguno)");

            if (Retardo > TimeSpan.Zero)
                await Task.Delay(Retardo, cancellationToken);

            if (JuntarAntesDeResponder > 0)
            {
                if (Interlocked.Increment(ref _enVuelo) >= JuntarAntesDeResponder)
                    _todasDentro.TrySetResult();

                // Con techo: si el portal las hubiera serializado, esperar sin límite dejaría la
                // prueba colgada en vez de en rojo.
                var esperando = await Task.WhenAny(
                    _todasDentro.Task, Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));

                if (esperando != _todasDentro.Task)
                    Interlocked.Increment(ref _serializadas);
            }

            // LA ROTACIÓN: el token recibido se gasta aquí mismo y no vuelve a valer.
            if (recibido is null || !_vigentes.TryRemove(recibido, out _))
            {
                Interlocked.Increment(ref _rechazados);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(
                        """{"success":false,"errorCode":"INVALID_REFRESH_TOKEN"}""",
                        Encoding.UTF8, "application/json")
                };
            }

            Interlocked.Increment(ref _renovaciones);

            var rotado = $"refresco-{Interlocked.Increment(ref _rotaciones)}";
            _vigentes[rotado] = 0;

            var accessToken = Token(DateTime.UtcNow.Add(VidaDelToken));
            _ultimoTokenDeAcceso = accessToken;

            // El cuerpo trae el refresco VACÍO, como el de verdad; el bueno va en la cabecera.
            var respuesta = Json(
                "{\"success\":true,\"data\":{\"accessToken\":\"" + accessToken +
                "\",\"refreshToken\":\"\"}}");

            respuesta.Headers.Add("Set-Cookie",
                $"{RefreshCookie.Name}={rotado}; Path=/; HttpOnly; Secure; SameSite=Strict");

            return respuesta;
        }

        private static HttpResponseMessage Json(string cuerpo) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
            };
    }

    private static PortalSessionTokens Almacen(ApiFalsa api) =>
        new(new AuthTokenRefresher(new FabricaDeClientes(api), NullLogger<AuthTokenRefresher>.Instance),
            NullLogger<PortalSessionTokens>.Instance);

    private sealed class FabricaDeClientes(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://signupapi.pruebas/")
            };
    }

    /// <summary>Un gateway que habla con la API falsa llevando el token que se le diga.</summary>
    private static AuthApiGateway GatewayDe(ApiFalsa api, string token) =>
        new(new FabricaDeClientes(api), new TokenFijo(token), NullLogger<AuthApiGateway>.Instance);

    /// <summary>
    /// Un gateway que responde al login con un token de acceso y, si se le pide, con el
    /// <c>Set-Cookie</c> del refresco — que es como lo entrega la API de verdad.
    /// </summary>
    private static AuthApiGateway GatewayQueEntregaTokens(string accessToken, string? refreshToken) =>
        new(new FabricaDeClientes(new HandlerFalso(_ =>
            {
                var respuesta = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"success\":true,\"data\":{\"accessToken\":\"" + accessToken +
                        "\",\"refreshToken\":\"\"}}",
                        Encoding.UTF8, "application/json")
                };

                if (refreshToken is not null)
                {
                    respuesta.Headers.Add("Set-Cookie",
                        $"{RefreshCookie.Name}={refreshToken}; Path=/; HttpOnly; Secure");
                }

                return respuesta;
            })),
            new TokenFijo(null), NullLogger<AuthApiGateway>.Instance);

    private sealed class HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class TokenFijo(string? token) : IAccessTokenProvider
    {
        public ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(token);
    }

    // ── El mundo del circuito ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// El manejador de las APIs resuelto desde un ámbito distinto al del circuito, que es
    /// exactamente como lo construye <c>IHttpClientFactory</c>.
    /// </summary>
    private sealed class MundoDelCircuito
    {
        private readonly IServiceScope  _ambitoDelCircuito;
        private readonly ApiAuthHandler _manejador;
        private readonly NavegadorFalso _navegador;

        public PortalSessionTokens Almacen              { get; }
        public string?             AutorizacionRecibida { get; private set; }

        public List<(string Uri, bool ForceLoad)> Navegaciones => _navegador.Navegaciones;

        public MundoDelCircuito(ApiFalsa api)
        {
            Almacen = RefrescoDeSesionTests.Almacen(api);

            var servicios = new ServiceCollection();
            servicios.AddLogging();
            servicios.AddPortalApiAuthHandler(LoginBizCenter, SalidaDelPortal);
            servicios.AddScoped<NavigationManager>(_ => new NavegadorFalso());
            servicios.AddScoped<AuthenticationStateProvider>(_ => new EstadoDelCircuito());

            var raiz              = servicios.BuildServiceProvider();
            var ambitoDeLaFabrica = raiz.CreateScope();
            _ambitoDelCircuito    = raiz.CreateScope();

            _navegador = (NavegadorFalso)_ambitoDelCircuito.ServiceProvider
                .GetRequiredService<NavigationManager>();

            raiz.GetRequiredService<CircuitServicesAccessor>().Services =
                _ambitoDelCircuito.ServiceProvider;

            // Se arma a mano con el almacén de esta prueba, pero con las MISMAS piezas que registra
            // el portal: lo que se prueba es el manejador, no el contenedor.
            _manejador = new ApiAuthHandler(
                ambitoDeLaFabrica.ServiceProvider.GetRequiredService<IHttpContextAccessor>(),
                raiz.GetRequiredService<CircuitServicesAccessor>(),
                Almacen,
                NullLogger<ApiAuthHandler>.Instance,
                LoginBizCenter,
                SalidaDelPortal)
            {
                InnerHandler = new HandlerFalso(peticion =>
                {
                    AutorizacionRecibida = peticion.Headers.Authorization?.ToString();
                    return new HttpResponseMessage(HttpStatusCode.OK);
                })
            };
        }

        public async Task<HttpResponseMessage> LlamarAsync(
            string accessToken, string? refreshToken, string? sessionId)
        {
            ((EstadoDelCircuito)_ambitoDelCircuito.ServiceProvider
                .GetRequiredService<AuthenticationStateProvider>()).Usuario =
                Usuario(accessToken, refreshToken, sessionId);

            using var invocador = new HttpMessageInvoker(_manejador, disposeHandler: false);
            return await invocador.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "https://api.pruebas/algo"), default);
        }
    }

    private sealed class NavegadorFalso : NavigationManager
    {
        public List<(string Uri, bool ForceLoad)> Navegaciones { get; } = [];

        public NavegadorFalso() => Initialize("https://portal.pruebas/", "https://portal.pruebas/");

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            Navegaciones.Add((uri, options.ForceLoad));
    }

    private sealed class EstadoDelCircuito : AuthenticationStateProvider
    {
        public ClaimsPrincipal Usuario { get; set; } = new();

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(Usuario));
    }

    // ── Contextos y usuarios ───────────────────────────────────────────────────────────────────

    /// <summary>El usuario tal y como sale de la cookie de sesión, con sus tres claims.</summary>
    private static ClaimsPrincipal Usuario(
        string? accessToken, string? refreshToken, string? sessionId)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "quien@ejemplo.com") };

        if (accessToken  is not null) claims.Add(new Claim(SessionExpiry.AccessTokenClaim,  accessToken));
        if (refreshToken is not null) claims.Add(new Claim(SessionExpiry.RefreshTokenClaim, refreshToken));
        if (sessionId    is not null) claims.Add(new Claim(SessionExpiry.SessionIdClaim,    sessionId));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie"));
    }

    private static DefaultHttpContext Contexto() => ContextoDe(new ClaimsPrincipal());

    private static DefaultHttpContext ContextoDe(ClaimsPrincipal usuario)
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddSingleton<IAuthenticationService>(new AutenticacionFalsa());

        return new DefaultHttpContext
        {
            RequestServices = servicios.BuildServiceProvider(),
            User            = usuario
        };
    }

    private static DefaultHttpContext Navegacion(
        string ruta, string accessToken, string? refreshToken, string? sessionId)
    {
        var contexto = ContextoDe(Usuario(accessToken, refreshToken, sessionId));

        contexto.Request.Method         = HttpMethods.Get;
        contexto.Request.Path           = ruta;
        contexto.Request.Headers.Accept = "text/html,application/xhtml+xml";

        return contexto;
    }

    /// <summary>Corre el middleware y dice si la petición siguió su camino.</summary>
    private static async Task<bool> Ejecutar(
        HttpContext contexto, AuthPortalOptions portal, PortalSessionTokens almacen)
    {
        var siguio = false;

        var middleware = new SessionExpiryMiddleware(
            _ => { siguio = true; return Task.CompletedTask; },
            portal,
            almacen,
            NullLogger<SessionExpiryMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);
        return siguio;
    }

    private static AutenticacionFalsa Autenticacion(HttpContext contexto) =>
        (AutenticacionFalsa)contexto.RequestServices.GetRequiredService<IAuthenticationService>();

    private static ClaimsPrincipal[] Firmadas(HttpContext contexto) =>
        [.. Autenticacion(contexto).Firmadas];

    /// <summary>
    /// Apunta las firmas y las salidas en vez de montar el esquema de cookie entero: lo que interesa
    /// es a quién se firma y si la sesión se limpió, no cómo se serializa la cookie.
    /// </summary>
    private sealed class AutenticacionFalsa : IAuthenticationService
    {
        public List<ClaimsPrincipal> Firmadas { get; } = [];
        public int                   Salidas  { get; private set; }

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
            HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            Salidas++;
            return Task.CompletedTask;
        }
    }

    // ── Tokens ─────────────────────────────────────────────────────────────────────────────────

    private static string TokenVivo()     => Token(DateTime.UtcNow.AddMinutes(15));
    private static string TokenCaducado() => Token(DateTime.UtcNow.AddMinutes(-1));

    /// <summary>
    /// Un JWT sin firmar. Nada del camino comprueba la firma —la comprobó la API al emitirlo—, así
    /// que basta con que se pueda leer y con que su <c>exp</c> diga la verdad. Lleva un identificador
    /// al azar para que dos tokens seguidos no salgan idénticos y una prueba de rotación pueda
    /// distinguirlos.
    /// </summary>
    private static string Token(DateTime caduca) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer:    "pruebas",
            audience:  "pruebas",
            claims:    [
                new Claim(JwtRegisteredClaimNames.Sub,   "un-usuario"),
                new Claim(JwtRegisteredClaimNames.Email, "quien@ejemplo.com"),
                new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString("N")),
                new Claim(ClaimTypes.Role, "Member")
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires:   caduca));
}
