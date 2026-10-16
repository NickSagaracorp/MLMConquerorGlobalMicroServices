using MLMConquerorGlobalEdition.Repository.Services.Links;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Una plantilla de enlace llega a todos los miembros, que la reparten. La guarda no deja arrancar
/// el BizCenter con una que no sea https, que meta el slug en el dominio o que traiga marcadores
/// que no entendemos.
/// </summary>
public class MyLinksOptionsGuardTests
{
    private static MyLinksOptions Con(params (string Key, string Label, string Url)[] enlaces) => new()
    {
        Links = enlaces.Select(e => new MyLinkTemplate { Key = e.Key, Label = e.Label, Url = e.Url }).ToList()
    };

    [Fact]
    public void Sin_enlaces_es_valido()
        => MyLinksOptionsGuard.Error(new MyLinksOptions()).Should().BeNull();

    [Theory]
    [InlineData("https://mwrlife.com/{slug}")]
    [InlineData("https://join.traveladvantage.com/{slug}")]
    [InlineData("https://mwracademy.com")]
    [InlineData("https://mwrevents.com/agenda?ref={slug}")]
    public void Acepta_https_con_el_slug_en_la_ruta_o_sin_slug(string url)
        => MyLinksOptionsGuard.Error(Con(("enlace", "Etiqueta", url))).Should().BeNull();

    [Theory]
    [InlineData("http://mwrlife.com/{slug}",                "https")]
    [InlineData("mwrlife.com/{slug}",                       "https")]
    [InlineData("javascript:alert(1)",                      "https")]
    [InlineData("https://{slug}.mwrlife.com/",              "ruta")]
    [InlineData("https://mwrlife.com/{slug}/{slug}",        "una vez")]
    [InlineData("https://mwrlife.com/{user}",               "{slug}")]
    [InlineData("https://usuario:clave@mwrlife.com/{slug}", "usuario")]
    public void Rechaza_la_url(string url, string motivo)
        => MyLinksOptionsGuard.Error(Con(("enlace", "Etiqueta", url))).Should().Contain(motivo);

    [Fact]
    public void Rechaza_claves_repetidas_sin_distinguir_mayusculas()
        => MyLinksOptionsGuard.Error(Con(
                ("mwrlife", "Uno", "https://mwrlife.com/{slug}"),
                ("MWRLIFE", "Dos", "https://mwrlife.online")))
            .Should().Contain("repetida");

    [Theory]
    [InlineData("con espacio")]
    [InlineData("")]
    public void Rechaza_claves_invalidas(string clave)
        => MyLinksOptionsGuard.Error(Con((clave, "Etiqueta", "https://mwrlife.com"))).Should().Contain("clave");

    [Fact]
    public void Rechaza_etiquetas_vacias_o_largas()
    {
        MyLinksOptionsGuard.Error(Con(("a", " ", "https://mwrlife.com"))).Should().Contain("etiqueta");
        MyLinksOptionsGuard.Error(Con(("a", new string('x', 61), "https://mwrlife.com"))).Should().Contain("etiqueta");
    }

    [Fact]
    public void Rechaza_mas_de_treinta_enlaces()
    {
        var muchos = Enumerable.Range(1, 31).Select(i => ($"e{i}", "Etiqueta", "https://mwrlife.com")).ToArray();
        MyLinksOptionsGuard.Error(Con(muchos)).Should().Contain("30");
    }

    [Fact]
    public void ThrowIfInvalid_lanza_con_el_motivo()
    {
        var act = () => MyLinksOptionsGuard.ThrowIfInvalid(Con(("a", "A", "http://mwrlife.com")));
        act.Should().Throw<InvalidOperationException>().WithMessage("*https*");
    }

    [Fact]
    public void Sabe_si_una_plantilla_es_personal()
    {
        new MyLinkTemplate { Url = "https://mwrlife.com/{slug}" }.IsPersonal.Should().BeTrue();
        new MyLinkTemplate { Url = "https://mwracademy.com" }.IsPersonal.Should().BeFalse();
    }
}
