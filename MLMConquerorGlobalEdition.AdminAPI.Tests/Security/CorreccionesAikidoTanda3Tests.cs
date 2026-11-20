using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.SharedKernel.Security;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Security;

/// <summary>Adjuntos en disco, HTML enriquecido y destino del Re-check (20-nov-2026).</summary>
public class CorreccionesAikidoTanda3Tests
{
    // ── Adjuntos ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("foto.PNG", ".png")]
    [InlineData("contrato.pdf", ".pdf")]
    [InlineData("..\\..\\web.config.txt", ".txt")]
    public void La_extension_permitida_sale_en_minusculas(string nombre, string esperada) =>
        AdjuntoEnDisco.ExtensionPermitida(nombre).Should().Be(esperada);

    [Theory]
    [InlineData("script.exe")]
    [InlineData("pagina.html")]
    [InlineData("sin-extension")]
    [InlineData("")]
    [InlineData(null)]
    public void Una_extension_fuera_de_la_lista_no_pasa(string? nombre) =>
        AdjuntoEnDisco.ExtensionPermitida(nombre).Should().BeNull();

    [Fact]
    public void El_nombre_nuevo_es_un_guid_con_la_extension()
    {
        var nombre = AdjuntoEnDisco.NombreNuevo(".pdf");
        nombre.Should().MatchRegex("^[0-9a-f]{32}\\.pdf$");
    }

    [Fact]
    public void La_ruta_queda_dentro_de_la_carpeta()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "adjuntos");
        var ruta = AdjuntoEnDisco.RutaDentroDe(carpeta, "abc.pdf");
        ruta.Should().StartWith(Path.GetFullPath(carpeta) + Path.DirectorySeparatorChar);
    }

    [Theory]
    [InlineData("../fuera.pdf")]
    [InlineData("..\\fuera.pdf")]
    [InlineData("sub/dentro.pdf")]
    [InlineData("..")]
    [InlineData("")]
    public void Un_nombre_con_ruta_no_se_escribe(string nombre)
    {
        var accion = () => AdjuntoEnDisco.RutaDentroDe(Path.GetTempPath(), nombre);
        accion.Should().Throw<InvalidOperationException>();
    }

    // ── HTML enriquecido ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("<p>hola<script>alert(1)</script></p>", "<p>hola</p>")]
    [InlineData("<img src=x onerror=alert(1)>", "<img src=\"x\">")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "<a>x</a>")]
    public void HtmlSeguro_quita_lo_que_ejecuta(string entrada, string esperado) =>
        HtmlSeguro.Limpiar(entrada).Should().Be(esperado);

    [Fact]
    public void HtmlSeguro_deja_el_formato() =>
        HtmlSeguro.Limpiar("<p><strong>Plan</strong> <em>Elite</em></p>")
            .Should().Be("<p><strong>Plan</strong> <em>Elite</em></p>");

    // ── Re-check de Entrax ────────────────────────────────────────────────────────────────

    [Fact]
    public void El_recheck_va_a_la_base_de_SignupAPI()
    {
        var destino = AdminEntraxSignupsController.DestinoDelRecheck(
            new Uri("https://signup.pruebas/"), "pago-1");
        destino.Should().Be(new Uri("https://signup.pruebas/api/v1/signups/entrax/pago-1/recheck"));
    }

    [Theory]
    [InlineData("//otro.host/x")]
    [InlineData("../../../x")]
    [InlineData("https://otro.host/")]
    public void Un_id_raro_no_saca_la_peticion_de_SignupAPI(string id)
    {
        var destino = AdminEntraxSignupsController.DestinoDelRecheck(new Uri("https://signup.pruebas/"), id);
        destino.Host.Should().Be("signup.pruebas");
        destino.AbsolutePath.Should().StartWith("/api/v1/signups/entrax/");
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    public void Un_id_que_sube_de_carpeta_no_sale(string id)
    {
        var accion = () => AdminEntraxSignupsController.DestinoDelRecheck(new Uri("https://signup.pruebas/"), id);
        accion.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Sin_direccion_base_no_sale()
    {
        var accion = () => AdminEntraxSignupsController.DestinoDelRecheck(null, "pago-1");
        accion.Should().Throw<InvalidOperationException>();
    }
}
