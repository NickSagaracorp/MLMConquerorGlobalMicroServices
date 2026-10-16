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
}
