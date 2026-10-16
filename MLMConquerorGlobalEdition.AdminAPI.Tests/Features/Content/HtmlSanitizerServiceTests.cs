using FluentAssertions;
using MLMConquerorGlobalEdition.AdminAPI.Services;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Content;

/// <summary>
/// La descripción de producto se guarda con lo que devuelve el sanitizador. Estas pruebas fijan su
/// comportamiento al pasar de HtmlSanitizer 8 a 9 (GHSA-j92c-7v7g-gj3f, el salto con
/// <c>&lt;template&gt;</c>) y de AngleSharp 0.17 a 1.x (GHSA-pgww-w46g-26qg, mXSS con annotation-xml).
/// </summary>
public class HtmlSanitizerServiceTests
{
    private readonly HtmlSanitizerService _sanitizer = new();

    [Fact]
    public void Conserva_el_formato_permitido()
    {
        var html = "<p><strong>Hola</strong> <em>mundo</em></p><ul><li>uno</li></ul>";

        _sanitizer.Sanitize(html).Should().Be(html);
    }

    [Theory]
    [InlineData("<script>alert(1)</script><p>x</p>")]
    [InlineData("<img src=\"https://a.example/x.png\" onerror=\"alert(1)\">")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>")]
    [InlineData("<template><img src=x onerror=alert(1)></template>")]
    [InlineData("<math><annotation-xml encoding=\"text/html\"><style><img src=x onerror=alert(1)></style></annotation-xml></math>")]
    [InlineData("<svg><a><foreignObject><img src=x onerror=alert(1)></foreignObject></a></svg>")]
    public void Quita_todo_lo_ejecutable(string html)
    {
        var limpio = _sanitizer.Sanitize(html);

        limpio.Should().NotContainEquivalentOf("<script");
        limpio.Should().NotContainEquivalentOf("onerror");
        limpio.Should().NotContainEquivalentOf("javascript:");
        limpio.Should().NotContainEquivalentOf("<template");
        limpio.Should().NotContainEquivalentOf("<math");
        limpio.Should().NotContainEquivalentOf("<svg");
    }

    [Fact]
    public void Los_enlaces_salen_con_noopener()
    {
        var limpio = _sanitizer.Sanitize("<a href=\"https://a.example\" target=\"_blank\">x</a>");

        limpio.Should().Contain("rel=\"noopener noreferrer\"");
        limpio.Should().Contain("href=\"https://a.example\"");
    }

    [Fact]
    public void Quita_las_propiedades_css_no_permitidas()
    {
        var limpio = _sanitizer.Sanitize("<p style=\"color: red; position: fixed\">x</p>");

        limpio.Should().Contain("color:");
        limpio.Should().NotContain("position");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Vacio_devuelve_cadena_vacia(string? html) =>
        _sanitizer.Sanitize(html).Should().BeEmpty();
}
