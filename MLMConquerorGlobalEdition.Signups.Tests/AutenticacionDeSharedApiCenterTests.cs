using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MLMConquerorGlobalEdition.SharedAPICenter.Controllers;
using MLMConquerorGlobalEdition.SharedAPICenter.Seguridad;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests;

/// <summary>
/// SharedAPICenter no usa JWT: los sistemas externos entran con X-Api-Key y las pasarelas con la
/// firma HMAC del cuerpo. Las dos puertas fallan cerradas.
/// </summary>
public class AutenticacionDeSharedApiCenterTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 20, 15, 0, 0, TimeSpan.Zero);
    private const string Secreto = "secreto-de-prueba-de-la-pasarela";
    private const string Llave = "llave-externa-de-prueba";

    [Theory]
    [InlineData(typeof(ExternalController), ApiKeyExternaHandler.Esquema)]
    [InlineData(typeof(WebhooksController), FirmaWebhookHandler.Esquema)]
    public void CadaControladorExigeSuEsquema(Type controlador, string esquema)
    {
        var atributo = controlador.GetCustomAttribute<AuthorizeAttribute>();
        atributo.Should().NotBeNull();
        atributo!.AuthenticationSchemes.Should().Be(esquema);

        controlador.GetMethods()
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Should().BeEmpty("ninguna acción de este servicio es anónima");
    }

    // ── API key ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ApiKey_correcta_entra()
    {
        var r = await AutenticarApiKey(Llave, cabecera: Llave);
        r.Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("otra-llave")]
    [InlineData("")]
    public async Task ApiKey_ausente_o_distinta_no_entra(string? cabecera)
    {
        var r = await AutenticarApiKey(Llave, cabecera);
        r.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ApiKey_sin_configurar_no_entra_nadie()
    {
        var r = await AutenticarApiKey(configurada: "", cabecera: "");
        r.Succeeded.Should().BeFalse();
    }

    // ── Firma de webhooks ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_firmado_entra_y_el_cuerpo_queda_legible()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{\"event\":\"payment.success\"}");
        var marca = Ahora.ToUnixTimeSeconds().ToString();
        var (r, ctx) = await AutenticarWebhook("stripe", Secreto, cuerpo, marca,
            FirmaWebhookHandler.Firmar(Secreto, marca, cuerpo));

        r.Succeeded.Should().BeTrue();
        ctx.Request.Body.Position.Should().Be(0, "el controlador tiene que poder leer el cuerpo");
        new StreamReader(ctx.Request.Body).ReadToEnd().Should().Be("{\"event\":\"payment.success\"}");
    }

    [Fact]
    public async Task Webhook_con_cuerpo_cambiado_no_entra()
    {
        var marca = Ahora.ToUnixTimeSeconds().ToString();
        var firma = FirmaWebhookHandler.Firmar(Secreto, marca, Encoding.UTF8.GetBytes("{\"amount\":1}"));
        var (r, _) = await AutenticarWebhook("stripe", Secreto,
            Encoding.UTF8.GetBytes("{\"amount\":1000}"), marca, firma);
        r.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Webhook_viejo_no_se_puede_repetir()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{}");
        var marca = Ahora.AddMinutes(-6).ToUnixTimeSeconds().ToString();
        var (r, _) = await AutenticarWebhook("stripe", Secreto, cuerpo, marca,
            FirmaWebhookHandler.Firmar(Secreto, marca, cuerpo));
        r.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Webhook_de_pasarela_sin_secreto_no_entra()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{}");
        var marca = Ahora.ToUnixTimeSeconds().ToString();
        var (r, _) = await AutenticarWebhook("braintree", secretoConfigurado: null, cuerpo, marca,
            FirmaWebhookHandler.Firmar(Secreto, marca, cuerpo));
        r.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Webhook_firmado_con_el_secreto_de_otra_pasarela_no_entra()
    {
        var cuerpo = Encoding.UTF8.GetBytes("{}");
        var marca = Ahora.ToUnixTimeSeconds().ToString();
        var (r, _) = await AutenticarWebhook("stripe", Secreto, cuerpo, marca,
            FirmaWebhookHandler.Firmar("otro-secreto", marca, cuerpo));
        r.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Webhook_sin_firma_no_entra()
    {
        var (r, _) = await AutenticarWebhook("stripe", Secreto, Encoding.UTF8.GetBytes("{}"),
            Ahora.ToUnixTimeSeconds().ToString(), firma: null);
        r.Succeeded.Should().BeFalse();
    }

    // ── Montaje ───────────────────────────────────────────────────────────────────────────

    private static async Task<AuthenticateResult> AutenticarApiKey(string configurada, string? cabecera)
    {
        var config = Config(new() { [ApiKeyExternaHandler.ClaveDeConfiguracion] = configurada });
        var handler = new ApiKeyExternaHandler(new Opciones(), NullLoggerFactory.Instance,
            UrlEncoder.Default, config);
        var ctx = new DefaultHttpContext();
        if (cabecera is not null) ctx.Request.Headers[ApiKeyExternaHandler.Cabecera] = cabecera;

        await handler.InitializeAsync(
            new AuthenticationScheme(ApiKeyExternaHandler.Esquema, null, typeof(ApiKeyExternaHandler)), ctx);
        return await handler.AuthenticateAsync();
    }

    private static async Task<(AuthenticateResult, HttpContext)> AutenticarWebhook(
        string pasarela, string? secretoConfigurado, byte[] cuerpo, string marca, string? firma)
    {
        var valores = new Dictionary<string, string?>();
        if (secretoConfigurado is not null)
            valores[FirmaWebhookHandler.ClaveDelSecreto(pasarela)] = secretoConfigurado;

        var handler = new FirmaWebhookHandler(new Opciones(), NullLoggerFactory.Instance,
            UrlEncoder.Default, Config(valores), new RelojFijo(Ahora));

        var ctx = new DefaultHttpContext();
        ctx.Request.RouteValues["provider"] = pasarela;
        ctx.Request.Body = new MemoryStream(cuerpo);
        ctx.Request.ContentLength = cuerpo.Length;
        ctx.Request.Headers[FirmaWebhookHandler.CabeceraMarca] = marca;
        if (firma is not null) ctx.Request.Headers[FirmaWebhookHandler.CabeceraFirma] = firma;

        await handler.InitializeAsync(
            new AuthenticationScheme(FirmaWebhookHandler.Esquema, null, typeof(FirmaWebhookHandler)), ctx);
        return (await handler.AuthenticateAsync(), ctx);
    }

    private static IConfiguration Config(Dictionary<string, string?> valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    private sealed class Opciones : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        public AuthenticationSchemeOptions CurrentValue { get; } = new();
        public AuthenticationSchemeOptions Get(string? name) => new();
        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
