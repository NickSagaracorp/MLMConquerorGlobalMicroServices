using Microsoft.Extensions.Configuration;
using MLMConquerorGlobalEdition.Repository.Services.Links;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// El BizCenter no arranca con una plantilla mala (R11). Esta prueba pasa el appsettings.json del
/// repositorio por la misma guarda, para que una plantilla mala no llegue a fusionarse.
/// </summary>
public class ConfiguracionDeEnlacesTests
{
    [Fact]
    public void El_appsettings_del_BizCenter_trae_plantillas_validas()
    {
        var ruta = Path.Combine(ReportesDb.RaizDelRepositorio(),
            "MLMConquerorGlobalEdition.BizCenter", "appsettings.json");
        var configuracion = new ConfigurationBuilder().AddJsonFile(ruta, optional: false).Build();

        var opciones = configuracion.GetSection(MyLinksOptions.SectionName).Get<MyLinksOptions>();

        opciones.Should().NotBeNull();
        MyLinksOptionsGuard.Error(opciones!).Should().BeNull();
        opciones!.Links.Should().HaveCount(10);
        opciones.Links.Count(l => l.IsPersonal).Should().Be(5);
    }

    /// <summary>
    /// Trampa de .NET: un appsettings.{Entorno}.json NO sustituye la lista, la fusiona POR ÍNDICE.
    /// Un entorno que declare un solo enlace pisa el primero y deja vivos los demás del base. Para
    /// cambiar la lista en un entorno hay que repetirla entera (o vaciar cada posición sobrante).
    /// </summary>
    [Fact]
    public void Un_appsettings_de_entorno_fusiona_los_enlaces_por_indice()
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MyLinks:Links:0:Key"] = "a", ["MyLinks:Links:0:Label"] = "A", ["MyLinks:Links:0:Url"] = "https://a.com",
                ["MyLinks:Links:1:Key"] = "b", ["MyLinks:Links:1:Label"] = "B", ["MyLinks:Links:1:Url"] = "https://b.com",
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MyLinks:Links:0:Key"] = "z", ["MyLinks:Links:0:Label"] = "Z", ["MyLinks:Links:0:Url"] = "https://z.com",
            })
            .Build();

        var opciones = configuracion.GetSection(MyLinksOptions.SectionName).Get<MyLinksOptions>()!;

        opciones.Links.Select(l => l.Key).Should().Equal("z", "b");
    }
}
