using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.SharedKernel.Billing;
using MLMConquerorGlobalEdition.Signups.Client.Payments;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Entrax;

/// <summary>
/// La pestaña de tarjeta del alta (Signup.razor y MemberJoin.razor) solo aparece si el servidor dice
/// <c>cardEnabled: true</c> en <c>payment-options</c>. Si no se puede leer, no aparece (falla cerrada).
/// Con la tarjeta oculta la pestaña por defecto es otra, también al cambiar de país o al volver de un QR.
/// El servidor manda siempre: aunque la pantalla la mostrara, SignupAPI la rechaza.
/// </summary>
public class PestanaDeTarjetaDelAltaTests
{
    private sealed class Api(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Peticion;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            Peticion = r;
            return Task.FromResult(responder(r));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json)
        => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpClient Cliente(HttpMessageHandler h) => new(h) { BaseAddress = new Uri("https://signup.pruebas/") };

    // ── Lo que lee la pantalla ──────────────────────────────────────────────

    [Fact]
    public async Task Lee_cardEnabled_y_los_paises_de_payment_options()
    {
        var api = new Api(_ => Json(HttpStatusCode.OK,
            """{"success":true,"data":{"entraxCountries":["RU"],"cardEnabled":true}}"""));

        var o = await SignupPaymentOptionsApi.GetAsync(Cliente(api));

        o.CardEnabled.Should().BeTrue();
        o.EntraxCountries.Should().Equal("RU");
        api.Peticion!.RequestUri!.AbsolutePath.Should().Be("/api/v1/signups/payment-options");
    }

    [Fact]
    public async Task Sin_cardEnabled_en_la_respuesta_la_tarjeta_no_se_ofrece()
        => (await SignupPaymentOptionsApi.GetAsync(Cliente(new Api(_ => Json(HttpStatusCode.OK,
                """{"success":true,"data":{"entraxCountries":["RU"]}}""")))))
            .CardEnabled.Should().BeFalse();

    [Fact]
    public async Task Si_no_se_puede_leer_ni_tarjeta_ni_Entrax()
    {
        var o = await SignupPaymentOptionsApi.GetAsync(Cliente(new Api(_ => throw new HttpRequestException("caido"))));

        o.CardEnabled.Should().BeFalse();
        o.EntraxCountries.Should().BeEmpty();
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "{}")]
    [InlineData(HttpStatusCode.InternalServerError, """{"success":false,"data":{"cardEnabled":true}}""")]
    [InlineData(HttpStatusCode.OK, "esto no es json")]
    [InlineData(HttpStatusCode.OK, """{"success":false,"data":{"entraxCountries":["RU"],"cardEnabled":true}}""")]
    public async Task Una_respuesta_que_no_es_un_exito_tampoco_la_ofrece(HttpStatusCode code, string cuerpo)
    {
        var o = await SignupPaymentOptionsApi.GetAsync(Cliente(new Api(_ => Json(code, cuerpo))));

        o.CardEnabled.Should().BeFalse();
        o.EntraxCountries.Should().BeEmpty();
    }

    [Fact]
    public async Task Los_paises_de_Entrax_salen_de_la_misma_lectura()
        => (await EntraxSignupApi.GetCountriesAsync(Cliente(new Api(_ => Json(HttpStatusCode.OK,
                """{"success":true,"data":{"entraxCountries":["RU"],"cardEnabled":false}}""")))))
            .Should().Equal("RU");

    // ── Qué pestaña queda elegida ───────────────────────────────────────────

    [Fact]
    public void Por_defecto_es_la_tarjeta_solo_si_esta_encendida()
    {
        SignupPaymentTabs.Default(cardEnabled: true).Should().Be(SignupPaymentTabs.Card);
        SignupPaymentTabs.Default(cardEnabled: false).Should().Be(SignupPaymentTabs.Token);
    }

    [Fact]
    public void Antes_de_leer_las_opciones_no_es_la_tarjeta()
        => SignupPaymentTabs.Initial.Should().NotBe(SignupPaymentTabs.Card);

    [Theory]
    [InlineData("card",   false, false, "token")]
    [InlineData("card",   true,  false, "card")]
    [InlineData("entrax", false, false, "token")]
    [InlineData("entrax", true,  false, "card")]
    [InlineData("entrax", false, true,  "entrax")]
    [InlineData("crypto", false, false, "crypto")]
    [InlineData("token",  true,  true,  "token")]
    [InlineData("",       false, false, "token")]
    [InlineData("otra",   false, false, "token")]
    public void Una_pestana_que_ya_no_se_ofrece_vuelve_a_la_de_por_defecto(
        string pestana, bool tarjeta, bool entrax, string queda)
        => SignupPaymentTabs.Normalize(pestana, tarjeta, entrax).Should().Be(queda);

    // ── El marcado de las dos páginas ───────────────────────────────────────

    private static string Pagina(string nombre)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        dir.Should().NotBeNull();
        return File.ReadAllText(Path.Combine(dir!.FullName, "MLMConquerorGlobalEdition.Signups.Client", "Pages", nombre));
    }

    private static string Marcado(string texto) => texto[..texto.IndexOf("@code", StringComparison.Ordinal)];

    private static string Codigo(string texto) => texto[texto.IndexOf("@code", StringComparison.Ordinal)..];

    [Theory]
    [InlineData("Signup.razor")]
    [InlineData("MemberJoin.razor")]
    public void La_pestana_y_el_formulario_de_tarjeta_solo_se_pintan_encendida(string pagina)
    {
        var marcado = Marcado(Pagina(pagina));

        // El botón de la pestaña va dentro de un @if (_cardEnabled).
        var boton = marcado.IndexOf("_paymentTab = SignupPaymentTabs.Card", StringComparison.Ordinal);
        boton.Should().BePositive("la pestaña de tarjeta sigue existiendo para cuando se encienda");
        var condicion = marcado.LastIndexOf("@if (_cardEnabled)", boton, StringComparison.Ordinal);
        condicion.Should().BePositive();
        marcado[condicion..boton].Should().NotContain("}", "el botón tiene que quedar dentro del bloque");

        // El formulario de la tarjeta, también.
        marcado.Should().Contain("@if (_paymentTab == SignupPaymentTabs.Card && _cardEnabled)");
        marcado.Should().NotContain("_paymentTab == \"card\"");
        marcado.Should().NotContain("_paymentTab = \"card\"");
    }

    [Theory]
    [InlineData("Signup.razor")]
    [InlineData("MemberJoin.razor")]
    public void El_boton_de_alta_no_se_habilita_con_la_tarjeta_apagada(string pagina)
        => Marcado(Pagina(pagina)).Should().Contain("(_paymentTab == SignupPaymentTabs.Card && !_cardEnabled)");

    [Theory]
    [InlineData("Signup.razor")]
    [InlineData("MemberJoin.razor")]
    public void Las_pestanas_elegidas_salen_de_las_opciones_del_servidor(string pagina)
    {
        var codigo = Codigo(Pagina(pagina));

        codigo.Should().Contain("SignupPaymentOptionsApi.GetAsync(api)", "una sola lectura que falla cerrada");
        codigo.Should().Contain("_cardEnabled");
        codigo.Should().Contain("private string _paymentTab").And.Contain("= SignupPaymentTabs.Initial;");
        codigo.Should().Contain("_paymentTab = SignupPaymentTabs.Default(_cardEnabled);");
        codigo.Should().NotContain("_paymentTab = \"card\"");
        codigo.Should().NotContain("_paymentTab     = \"card\"");

        // Al cambiar de país (desplegable y ventana) la pestaña se revisa contra lo que se ofrece.
        Regex.Matches(codigo, @"_paymentTab\s*=\s*SignupPaymentTabs\.Normalize\(_paymentTab, _cardEnabled, EntraxOffered\)")
            .Count.Should().BeGreaterThanOrEqualTo(2);
        codigo.Should().NotContain("_paymentTab == \"entrax\" && !EntraxOffered) _paymentTab");

        // Con la tarjeta apagada la página no intenta tokenizar ni enviar la tarjeta.
        codigo.Should().Contain("if (paymentTab == SignupPaymentTabs.Card && !_cardEnabled)");
    }
}
