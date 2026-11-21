using Bunit;
using MLMConquerorGlobalEdition.SharedComponents.Lite;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests;

/// <summary>
/// La descripción de un plan se pinta en el alta pública: el componente entrega como marcado solo
/// lo que devolvió el saneador.
/// </summary>
public class HtmlSaneadoTests : BunitContext
{
    [Fact]
    public void Pinta_el_formato_y_quita_lo_que_ejecuta()
    {
        var cut = Render<HtmlSaneado>(p => p.Add(c => c.Html,
            "<p><strong>Elite</strong></p><script>alert(1)</script><img src=x onerror=alert(1)>"));

        cut.Markup.Should().Contain("<p><strong>Elite</strong></p>");
        cut.Markup.Should().NotContain("<script").And.NotContain("onerror");
    }

    [Fact]
    public void Vuelve_a_limpiar_cuando_cambia_el_html()
    {
        var cut = Render<HtmlSaneado>(p => p.Add(c => c.Html, "<em>uno</em>"));
        cut.Render(p => p.Add(c => c.Html, "<a href=\"javascript:alert(1)\">dos</a>"));

        cut.Markup.Should().NotContain("javascript:").And.Contain("dos");
    }

    [Fact]
    public void Sin_html_no_pinta_nada() =>
        Render<HtmlSaneado>(p => p.Add(c => c.Html, null)).Markup.Should().BeEmpty();
}
