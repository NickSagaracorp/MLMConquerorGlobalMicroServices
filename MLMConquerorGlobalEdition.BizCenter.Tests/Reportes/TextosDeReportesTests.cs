using System.Xml.Linq;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Las claves del Plan J existen y no están vacías en los nueve idiomas. Cada bloque añade las
/// suyas a <see cref="Claves"/>.
/// </summary>
public class TextosDeReportesTests
{
    private static readonly string[] Culturas = { "", ".es", ".pt", ".fr", ".it", ".de", ".ko", ".zh", ".ka" };

    public static readonly string[] Claves =
    {
        // Bloque 1 — Mis enlaces
        "Sidebar.MyLinks", "PageTitle.MyLinks", "MyLinks.Personal", "MyLinks.Corporate",
        "MyLinks.Copy", "MyLinks.Copied", "MyLinks.NoSlug", "MyLinks.GoToProfile", "MyLinks.LoadFailed",
        // (claves de los bloques siguientes)
    };

    public static IEnumerable<object[]> Archivos() => Culturas.Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Archivos))]
    public void Cada_idioma_tiene_las_claves_del_plan_J_y_no_estan_vacias(string cultura)
    {
        var ruta = Path.Combine(ReportesDb.RaizDelRepositorio(), "MLMConquerorGlobalEdition.SharedComponents",
            "Resources", $"SharedResources{cultura}.resx");
        var datos = XDocument.Load(ruta).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);

        Claves.Where(k => !datos.ContainsKey(k)).Should().BeEmpty($"SharedResources{cultura}.resx tiene que traerlas");
        Claves.Where(k => datos.TryGetValue(k, out var v) && string.IsNullOrWhiteSpace(v)).Should().BeEmpty();
    }
}
