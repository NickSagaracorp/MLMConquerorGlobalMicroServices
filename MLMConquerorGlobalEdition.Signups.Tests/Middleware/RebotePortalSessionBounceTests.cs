using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Signups.Middleware;

namespace MLMConquerorGlobalEdition.Signups.Tests.Middleware;

/// <summary>
/// CARGAR EL ASISTENTE DE ALTA TIENE QUE MATAR CUALQUIER SESIÓN ABIERTA EN ESE NAVEGADOR. Esta es LA
/// MITAD DE LA APLICACIÓN DE ALTA: que el navegador salga hacia el cierre de sesión de cada portal,
/// que salga UNA SOLA VEZ por portal, que vuelva con el patrocinador entero y —lo que manda por
/// encima de todo— QUE EL ALTA SE ABRA IGUAL SI UN PORTAL NO CONTESTA.
///
/// EL ESCENARIO: en un evento se dan de alta varias personas seguidas en el mismo ordenador. La
/// persona A termina y se levanta sin salir; la persona B se sienta y abre el alta, casi siempre por
/// el enlace del sitio replicado de su patrocinador. Con la sesión de A viva, a B le basta con
/// teclear cualquier dirección del portal para estar dentro de la cuenta de A.
///
/// LO QUE NO SE PRUEBA AQUÍ: que la sesión muera de verdad. Eso ocurre en el portal, en su
/// <c>/account/logout</c>, y está en <c>BizCenter.Tests/CierreDeSesionAlAbrirElAltaTests</c>. Aquí
/// solo se prueba que el navegador llegue allí, y que llegue las veces justas.
/// </summary>
public class RebotePortalSessionBounceTests
{
    private const string Alta        = "https://alta.ejemplo.com";
    private const string CierreBiz   = "https://portal.ejemplo.com/account/logout";
    private const string PulsoBiz    = "https://portal.ejemplo.com/health";
    private const string CierreAdmin = "https://admin.ejemplo.com/account/logout";
    private const string PulsoAdmin  = "https://admin.ejemplo.com/health";

    private static PortalSessionBounceOptions ConLosDosPortales() => new()
    {
        PublicBaseUrl = Alta,
        Portals =
        [
            new PortalStopOptions { Name = "BizCenterWeb", SignOutUrl = CierreBiz,   ProbeUrl = PulsoBiz  },
            new PortalStopOptions { Name = "AdminWeb",     SignOutUrl = CierreAdmin, ProbeUrl = PulsoAdmin }
        ]
    };

    private static PortalSessionBounceOptions ConUnPortal() => ConLosDosPortales() with
    {
        Portals =
        [
            new PortalStopOptions { Name = "BizCenterWeb", SignOutUrl = CierreBiz, ProbeUrl = PulsoBiz }
        ]
    };

    // ===============================================================================================
    //  1. El rebote ocurre, y conserva el patrocinador
    // ===============================================================================================

    /// <summary>
    /// LA PRUEBA DEL ESCENARIO. Alguien abre el alta: antes de pintar nada, el navegador se va al
    /// cierre de sesión del portal, con el destino de vuelta colgado.
    /// </summary>
    [Fact]
    public async Task AbrirElAlta_MandaElNavegadorAlCierreDeSesionDelPortal()
    {
        var mundo = new MundoDePruebas(ConUnPortal(), "/ambassador-join");

        var llegoALaPagina = await mundo.EjecutarAsync();

        llegoALaPagina.Should().BeFalse("la petición acaba en la redirección, no en la página");
        mundo.Destino.Should().StartWith($"{CierreBiz}?returnUrl=");
    }

    /// <summary>
    /// LA RUTA QUE DE VERDAD SE USA EN UN EVENTO: el enlace del sitio replicado del patrocinador. El
    /// patrocinador tiene que volver ENTERO — perderlo aquí sería reproducir, por otro camino, el
    /// mismo fallo que este trabajo viene a cerrar: un alta guardada sin patrocinador.
    /// </summary>
    [Fact]
    public async Task ElEnlaceDelPatrocinador_VuelveEnteroYConSuQuery()
    {
        var mundo = new MundoDePruebas(
            ConUnPortal(), "/ambassador-join/AMB-320189", query: "?utm_source=evento");

        await mundo.EjecutarAsync();

        mundo.DestinoDeVuelta.Should().Be(
            $"{Alta}/ambassador-join/AMB-320189?utm_source=evento&portal_session=1");
    }

    /// <summary>Las dos pantallas de alta, no solo la de embajador.</summary>
    [Theory]
    [InlineData("/ambassador-join")]
    [InlineData("/ambassador-join/AMB-320189")]
    [InlineData("/member-join")]
    [InlineData("/member-join/AMB-320189")]
    public async Task LasDosPantallasDeAltaRebotan(string ruta)
    {
        var mundo = new MundoDePruebas(ConUnPortal(), ruta);

        (await mundo.EjecutarAsync()).Should().BeFalse();
        mundo.Destino.Should().StartWith(CierreBiz);
    }

    // ===============================================================================================
    //  2. Una sola vez, y ni un bucle
    // ===============================================================================================

    /// <summary>
    /// EL BUCLE ES LO ÚNICO QUE PODRÍA DEJAR SIN ALTA A UNA SALA ENTERA. Con la marca ya puesta y un
    /// solo portal, no se vuelve a rebotar: se sigue a la página.
    /// </summary>
    [Fact]
    public async Task ConLaMarcaYaPuesta_NoSeRebotaOtraVez()
    {
        var mundo = new MundoDePruebas(
            ConUnPortal(), "/ambassador-join", query: "?portal_session=1");

        var llegoALaPagina = await mundo.EjecutarAsync();

        llegoALaPagina.Should().BeTrue();
        mundo.Destino.Should().BeEmpty();
        mundo.Pulsos.Should().BeEmpty("ni siquiera se pregunta por un portal ya visitado");
    }

    /// <summary>
    /// EL RECORRIDO ENTERO, PASO A PASO, tal y como lo recorre un navegador de verdad: dos portales,
    /// dos saltos, y a la tercera vuelta la página. Es la prueba de que esto TERMINA.
    /// </summary>
    [Fact]
    public async Task ElRecorridoDeLosDosPortales_TerminaEnLaPaginaYNoAntes()
    {
        var opciones = ConLosDosPortales();

        var primero = new MundoDePruebas(opciones, "/ambassador-join/AMB-320189");
        (await primero.EjecutarAsync()).Should().BeFalse();
        primero.Destino.Should().StartWith(CierreBiz);
        primero.DestinoDeVuelta.Should().Be($"{Alta}/ambassador-join/AMB-320189?portal_session=1");

        var segundo = new MundoDePruebas(
            opciones, "/ambassador-join/AMB-320189", query: "?portal_session=1");
        (await segundo.EjecutarAsync()).Should().BeFalse();
        segundo.Destino.Should().StartWith(CierreAdmin, "administración es el segundo del recorrido");
        segundo.DestinoDeVuelta.Should().Be($"{Alta}/ambassador-join/AMB-320189?portal_session=2");

        var tercero = new MundoDePruebas(
            opciones, "/ambassador-join/AMB-320189", query: "?portal_session=2");
        (await tercero.EjecutarAsync()).Should().BeTrue("ya no queda ningún portal al que ir");
        tercero.Destino.Should().BeEmpty();
    }

    /// <summary>
    /// La marca no se acumula por el camino: en el segundo salto hay UNA sola, la nueva.
    /// </summary>
    [Fact]
    public async Task LaMarcaNoSeAcumulaEntreSaltos()
    {
        var mundo = new MundoDePruebas(
            ConLosDosPortales(), "/ambassador-join", query: "?portal_session=1");

        await mundo.EjecutarAsync();

        mundo.DestinoDeVuelta.Should().Be($"{Alta}/ambassador-join?portal_session=2");
        mundo.DestinoDeVuelta!.Split("portal_session").Should().HaveCount(2);
    }

    /// <summary>
    /// UNA MARCA FUERA DE RANGO NO SALTA EL CIERRE. Sin acotarla, pegar un número grande al enlace
    /// apagaría la protección entera desde la barra de direcciones.
    /// </summary>
    [Theory]
    [InlineData("?portal_session=99")]
    [InlineData("?portal_session=-1")]
    [InlineData("?portal_session=daaa")]
    public async Task UnaMarcaFueraDeRango_NoSaltaElCierre(string query)
    {
        var mundo = new MundoDePruebas(ConUnPortal(), "/ambassador-join", query: query);

        (await mundo.EjecutarAsync()).Should().BeFalse("se rebota como si no hubiera marca");
        mundo.Destino.Should().StartWith(CierreBiz);
    }

    // ===============================================================================================
    //  3. EL PORTAL CAÍDO — la regla que manda sobre todas las demás
    // ===============================================================================================

    /// <summary>
    /// EL ALTA SE ABRE IGUAL SI EL PORTAL NO CONTESTA, y esta es la prueba que más importa de este
    /// archivo. El rebote es una navegación del navegador y una navegación no tiene plan B: mandar
    /// el navegador a un portal caído deja a quien venía a darse de alta en la pantalla de error del
    /// navegador. En un evento, a la sala entera a la vez. Se decide ANTES de soltarlo, aquí, y con
    /// el portal caído se abre el alta sin cerrar su sesión: es fallar hacia el lado del negocio a
    /// sabiendas, y queda avisado en el registro.
    /// </summary>
    [Fact]
    public async Task ConElPortalCaido_ElAltaSeAbreIgual()
    {
        var mundo = new MundoDePruebas(ConUnPortal(), "/ambassador-join", portalEnPie: false);

        var llegoALaPagina = await mundo.EjecutarAsync();

        llegoALaPagina.Should().BeTrue("el alta NUNCA se queda bloqueada por un portal");
        mundo.Destino.Should().BeEmpty();
    }

    /// <summary>
    /// Un portal que TARDA MÁS DE LA CUENTA es un portal caído a estos efectos. El sondeo corre
    /// delante de cada carga del alta: más vale saltarse el cierre de un portal lento que hacer
    /// esperar a una sala entera mirando una pantalla en blanco.
    /// </summary>
    [Fact]
    public async Task ConElPortalQueNoContestaATiempo_ElAltaSeAbreIgual()
    {
        var mundo = new MundoDePruebas(
            ConUnPortal() with { ProbeTimeoutMilliseconds = 30 },
            "/ambassador-join",
            pulso: new ApiLenta(TimeSpan.FromSeconds(5)));

        (await mundo.EjecutarAsync()).Should().BeTrue();
        mundo.Destino.Should().BeEmpty();
    }

    /// <summary>
    /// QUE UN PORTAL ESTÉ CAÍDO NO EXCUSA AL OTRO. Con el centro de negocios sin contestar, el
    /// navegador sigue yendo a cerrar la sesión de administración, que es la más peligrosa de las
    /// dos.
    /// </summary>
    [Fact]
    public async Task ConUnPortalCaido_SeSaltaESEYSeSigueConElSiguiente()
    {
        var mundo = new MundoDePruebas(
            ConLosDosPortales(), "/ambassador-join", pulso: new ApiSelectiva(caida: PulsoBiz));

        (await mundo.EjecutarAsync()).Should().BeFalse();
        mundo.Destino.Should().StartWith(CierreAdmin);
        mundo.DestinoDeVuelta.Should().Be($"{Alta}/ambassador-join?portal_session=2",
            "el recorrido se coloca DESPUÉS del que se saltó, no antes: no se reintenta");
    }

    /// <summary>
    /// Cualquier respuesta HTTP cuenta como "en pie", también un 500. Lo que se pregunta no es si el
    /// portal funciona bien: es si hay alguien escuchando que vaya a atender la navegación en vez de
    /// dejar al navegador con un error de conexión.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CualquierRespuestaDelPortalCuentaComoEnPie(HttpStatusCode estado)
    {
        var mundo = new MundoDePruebas(
            ConUnPortal(), "/ambassador-join", pulso: new ApiQueResponde(estado));

        (await mundo.EjecutarAsync()).Should().BeFalse();
        mundo.Destino.Should().StartWith(CierreBiz);
    }

    /// <summary>
    /// El pulso se pregunta UNA VEZ POR VENTANA y no una por visita. Sin esto habría un sondeo por
    /// cada carga de la pantalla, que en un evento son muchas.
    /// </summary>
    [Fact]
    public async Task ElPulsoSePreguntaUnaVezPorVentana()
    {
        var pulso  = new ApiQueResponde(HttpStatusCode.OK);
        var mundo  = new MundoDePruebas(ConUnPortal(), "/ambassador-join", pulso: pulso);

        await mundo.EjecutarAsync();
        await new MundoDePruebas(ConUnPortal(), "/ambassador-join", pulso: pulso,
            reachability: mundo.Reachability).EjecutarAsync();
        await new MundoDePruebas(ConUnPortal(), "/ambassador-join", pulso: pulso,
            reachability: mundo.Reachability).EjecutarAsync();

        pulso.Peticiones.Should().Be(1);
    }

    // ===============================================================================================
    //  4. Lo que NO se toca
    // ===============================================================================================

    /// <summary>
    /// Lo que no es el navegador CARGANDO la página se deja pasar: el WebSocket del circuito, los
    /// recursos de <c>/_framework</c> y las llamadas del propio asistente a la API. Cortar
    /// cualquiera de esos dejaría el alta a medias, y peor: a mitad de rellenar el formulario, con
    /// todo lo tecleado por el suelo.
    /// </summary>
    [Fact]
    public async Task NoTocaLoQueNoEsUnaNavegacionDelNavegador()
    {
        var noEsHtml = new MundoDePruebas(ConUnPortal(), "/ambassador-join", acepta: "*/*");
        (await noEsHtml.EjecutarAsync()).Should().BeTrue();

        var noEsGet = new MundoDePruebas(
            ConUnPortal(), "/ambassador-join", metodo: HttpMethods.Post);
        (await noEsGet.EjecutarAsync()).Should().BeTrue();

        var elCircuito = new MundoDePruebas(ConUnPortal(), "/_blazor", acepta: "*/*");
        (await elCircuito.EjecutarAsync()).Should().BeTrue();
    }

    /// <summary>El resto de la aplicación de alta no rebota.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/health")]
    [InlineData("/css/joinpage.css")]
    // Una ruta que solo EMPIEZA igual no es la pantalla de alta: se compara por segmentos.
    [InlineData("/ambassador-joinotracosa")]
    [InlineData("/member-joinotracosa")]
    public async Task NoTocaNingunaOtraRuta(string ruta)
    {
        var mundo = new MundoDePruebas(ConUnPortal(), ruta);

        (await mundo.EjecutarAsync()).Should().BeTrue();
        mundo.Destino.Should().BeEmpty();
    }

    /// <summary>
    /// Un despliegue sin portales configurados —el alta servida desde otro dominio registrable,
    /// donde el rebote no puede funcionar— arranca y se comporta como si nada de esto existiera.
    /// </summary>
    [Fact]
    public async Task SinPortalesConfigurados_ElAltaSeAbreSinRebotar()
    {
        var mundo = new MundoDePruebas(
            ConUnPortal() with { Portals = [] }, "/ambassador-join");

        (await mundo.EjecutarAsync()).Should().BeTrue();
        mundo.Destino.Should().BeEmpty();
    }

    /// <summary>Y con el interruptor apagado, tampoco.</summary>
    [Fact]
    public async Task ApagadoEnConfiguracion_ElAltaSeAbreSinRebotar()
    {
        var mundo = new MundoDePruebas(
            ConUnPortal() with { Enabled = false }, "/ambassador-join");

        (await mundo.EjecutarAsync()).Should().BeTrue();
        mundo.Destino.Should().BeEmpty();
    }

    // ===============================================================================================
    //  5. El cableado avisa al arrancar si está mal escrito
    // ===============================================================================================

    /// <summary>
    /// Un portal declarado con una dirección RELATIVA rompe al arrancar y no en la primera visita.
    /// El navegador va a OTRO ORIGEN: una dirección relativa produciría un rebote a ninguna parte, y
    /// eso solo se sabría cuando alguien lo contara.
    /// </summary>
    [Fact]
    public void UnPortalConDireccionRelativa_FallaAlArrancar()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PortalSessionBounce:Portals:0:Name"]       = "BizCenterWeb",
                ["PortalSessionBounce:Portals:0:SignOutUrl"] = "/account/logout"
            })
            .Build();

        var servicios = new ServiceCollection();

        servicios.Invoking(s => s.AddPortalSessionBounce(configuracion))
                 .Should().Throw<InvalidOperationException>()
                 .WithMessage("*absoluta*");
    }

    /// <summary>Y sin sección de configuración arranca, que es un despliegue sin rebote.</summary>
    [Fact]
    public void SinSeccionDeConfiguracion_Arranca()
    {
        var servicios = new ServiceCollection().AddLogging();

        servicios.Invoking(s => s.AddPortalSessionBounce(
                     new ConfigurationBuilder().Build()))
                 .Should().NotThrow();
    }

    // ===============================================================================================
    //  Ayudas
    // ===============================================================================================

    /// <summary>
    /// Una petición a la aplicación de alta con el middleware del rebote montado encima, y los
    /// portales contestando —o no— lo que se le diga.
    /// </summary>
    private sealed class MundoDePruebas
    {
        public DefaultHttpContext  Contexto     { get; }
        public PortalReachability  Reachability { get; }

        private readonly PortalSessionBounceOptions _opciones;
        private readonly ApiQueResponde?            _sondeado;

        public MundoDePruebas(
            PortalSessionBounceOptions opciones,
            string                     ruta,
            string                     query        = "",
            string                     acepta       = "text/html,application/xhtml+xml",
            string                     metodo       = "GET",
            bool                       portalEnPie  = true,
            HttpMessageHandler?        pulso        = null,
            PortalReachability?        reachability = null)
        {
            _opciones = opciones;

            Contexto = new DefaultHttpContext();
            Contexto.Request.Method         = metodo;
            Contexto.Request.Path           = ruta;
            Contexto.Request.QueryString    = new QueryString(query);
            Contexto.Request.Headers.Accept = acepta;
            Contexto.Request.Scheme         = "https";
            Contexto.Request.Host           = new HostString("alta.ejemplo.com");

            var manejador = pulso
                         ?? (portalEnPie
                                ? new ApiQueResponde(HttpStatusCode.OK)
                                : (HttpMessageHandler)new ApiCaida());

            _sondeado = manejador as ApiQueResponde;

            Reachability = reachability ?? new PortalReachability(
                new FabricaDeClientes(manejador),
                opciones,
                NullLogger<PortalReachability>.Instance);
        }

        /// <summary>A dónde se redirigió, o cadena vacía si no hubo redirección.</summary>
        public string Destino => Contexto.Response.Headers.Location.ToString();

        /// <summary>El destino de vuelta que viaja dentro de la dirección de salida, ya sin escapar.</summary>
        public string? DestinoDeVuelta
        {
            get
            {
                var marca = Destino.IndexOf("returnUrl=", StringComparison.Ordinal);
                return marca < 0
                    ? null
                    : Uri.UnescapeDataString(Destino[(marca + "returnUrl=".Length)..]);
            }
        }

        /// <summary>A qué portales se les llegó a tomar el pulso.</summary>
        public IReadOnlyList<string> Pulsos => _sondeado?.Urls ?? [];

        /// <summary>Corre el middleware y dice si la petición llegó a la página.</summary>
        public async Task<bool> EjecutarAsync()
        {
            var llegoALaPagina = false;

            var middleware = new PortalSessionBounceMiddleware(
                _ => { llegoALaPagina = true; return Task.CompletedTask; },
                _opciones,
                Reachability,
                NullLogger<PortalSessionBounceMiddleware>.Instance);

            await middleware.InvokeAsync(Contexto);

            return llegoALaPagina;
        }
    }

    /// <summary>Un portal que contesta lo que se le diga, apuntando a quién le preguntaron.</summary>
    private sealed class ApiQueResponde(HttpStatusCode estado) : HttpMessageHandler
    {
        public List<string> Urls        { get; } = [];
        public int          Peticiones  => Urls.Count;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri?.ToString() ?? string.Empty);
            return Task.FromResult(new HttpResponseMessage(estado));
        }
    }

    /// <summary>Un portal que no está.</summary>
    private sealed class ApiCaida : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("no hay nadie escuchando");
    }

    /// <summary>Un portal que tarda más de lo que nadie va a esperar.</summary>
    private sealed class ApiLenta(TimeSpan cuanto) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(cuanto, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    /// <summary>Uno en pie y otro caído, para el recorrido de dos portales.</summary>
    private sealed class ApiSelectiva(string caida) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            request.RequestUri?.ToString() == caida
                ? throw new HttpRequestException("no hay nadie escuchando")
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }

    private sealed class FabricaDeClientes(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
