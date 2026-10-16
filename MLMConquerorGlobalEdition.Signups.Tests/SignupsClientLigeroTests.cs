using System.Xml.Linq;

namespace MLMConquerorGlobalEdition.Signups.Tests;

/// <summary>
/// El alta pública es un WebAssembly: todo lo que referencia Signups.Client se descarga en el
/// navegador de quien se da de alta. Solo usa un componente compartido (CryptoDepositPicker), que vive
/// en SharedComponents.Lite; SharedComponents, con Syncfusion y los componentes de los portales, no
/// entra. Se lee el .csproj porque una referencia de proyecto nueva es justo lo que hay que vigilar.
/// </summary>
public class SignupsClientLigeroTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MLMConquerorGlobalEdition.slnx"))) dir = dir.Parent;
        dir.Should().NotBeNull();
        return dir!.FullName;
    }

    private static XDocument Proyecto(string nombre) =>
        XDocument.Load(Path.Combine(Root(), nombre, nombre + ".csproj"));

    private static IEnumerable<string> Referencias(XDocument p, string elemento) =>
        p.Descendants(elemento).Select(e => (string?)e.Attribute("Include") ?? string.Empty);

    /// <summary>"..\X\X.csproj" → "X", con barras de Windows o no.</summary>
    private static string NombreDeProyecto(string include) =>
        Path.GetFileNameWithoutExtension(include.Replace('\\', '/').Split('/').Last());

    [Fact]
    public void Signups_Client_no_referencia_SharedComponents_sino_la_libreria_ligera()
    {
        var proyectos = Referencias(Proyecto("MLMConquerorGlobalEdition.Signups.Client"), "ProjectReference")
            .Select(NombreDeProyecto)
            .ToList();

        proyectos.Should().NotContain("MLMConquerorGlobalEdition.SharedComponents");
        proyectos.Should().Contain("MLMConquerorGlobalEdition.SharedComponents.Lite");
    }

    [Theory]
    [InlineData("MLMConquerorGlobalEdition.Signups.Client")]
    [InlineData("MLMConquerorGlobalEdition.SharedComponents.Lite")]
    public void Ni_el_alta_ni_la_libreria_ligera_llevan_Syncfusion(string nombre)
        => Referencias(Proyecto(nombre), "PackageReference")
            .Should().NotContain(r => r.StartsWith("Syncfusion", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void La_libreria_ligera_solo_depende_de_SharedKernel()
        => Referencias(Proyecto("MLMConquerorGlobalEdition.SharedComponents.Lite"), "ProjectReference")
            .Select(NombreDeProyecto)
            .Should().Equal("MLMConquerorGlobalEdition.SharedKernel");
}
