using System.Text.RegularExpressions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Regla: toda ruta de árbol (HierarchyPath) acaba en "/". Las lecturas ya lo garantizan con
/// TreePrefix; esto vigila a quien ESCRIBE rutas. Dos comprobaciones:
/// (1) un literal interpolado asignado a HierarchyPath acaba en "/";
/// (2) los sitios que escriben HierarchyPath son los que se revisaron (Plan J). Uno nuevo hace
/// fallar la prueba: hay que comprobar que su ruta acaba en "/" y añadirlo aquí.
/// Lo que la prueba NO ve es el valor de una variable (newPath, relative, ChildPath...): eso se
/// revisó a mano una vez y queda anotado en el plan. Tampoco ve un ExecuteUpdate con
/// SetProperty(x => x.HierarchyPath, ...); hoy no hay ninguno. El barrido incluye Scripts/.
/// </summary>
public class RutasConBarraFinalTests
{
    private static readonly Regex Escritura = new(@"\bHierarchyPath\s*=(?!=)\s*(?<valor>[^;,\r\n]*)", RegexOptions.Compiled);

    /// <summary>Archivo (relativo, con "/") → cuántas escrituras de HierarchyPath tiene.</summary>
    private static readonly Dictionary<string, int> Revisados = new()
    {
        // Plan P: el binario solo lo escribe DualTeamNodes (altas, raices, movimientos y retiradas).
        ["MLMConquerorGlobalEdition.Repository/Queries/DualTeamNodes.cs"]                                            = 4,
        // Arbol de inscripcion (GenealogyTree).
        ["MLMConquerorGlobalEdition.Repository/Seeders/RootAmbassadorSeeder.cs"]                                     = 1,
        ["MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs"] = 1,
        ["MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupMember/SignupMemberHandler.cs"]         = 1,
    };

    private static IEnumerable<(string Archivo, string Valor)> Escrituras()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        var sep  = Path.DirectorySeparatorChar;

        foreach (var proyecto in Directory.GetDirectories(raiz, "MLMConquerorGlobalEdition.*")
                     .Where(d => !d.EndsWith(".Tests", StringComparison.Ordinal))
                     .Append(Path.Combine(raiz, "Scripts"))
                     .Where(Directory.Exists))
        foreach (var archivo in Directory.EnumerateFiles(proyecto, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{sep}obj{sep}") || archivo.Contains($"{sep}bin{sep}")
                || archivo.Contains($"{sep}Migrations{sep}"))
                continue;

            var relativo = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            foreach (System.Text.RegularExpressions.Match m in Escritura.Matches(File.ReadAllText(archivo)))
                yield return (relativo, m.Groups["valor"].Value.Trim());
        }
    }

    [Fact]
    public void Un_literal_asignado_a_HierarchyPath_acaba_en_barra()
    {
        var literalesSinBarra = Escrituras()
            .Where(e => e.Valor.StartsWith("$\"", StringComparison.Ordinal)
                     && !e.Valor.EndsWith("/\"", StringComparison.Ordinal)
                     // {relative} es el resto de una ruta que ya existía (desde la barra siguiente al
                     // nodo movido hasta el final), así que acaba en "/" como ella (PlaceMemberHandler).
                     && !e.Valor.EndsWith("/{relative}\"", StringComparison.Ordinal))
            .Select(e => $"{e.Archivo}: {e.Valor}")
            .ToList();

        literalesSinBarra.Should().BeEmpty("una ruta de árbol tiene la forma \"/A/B/\" y acaba siempre en barra");
    }

    [Fact]
    public void Los_sitios_que_escriben_HierarchyPath_son_los_revisados()
    {
        var actuales = Escrituras().GroupBy(e => e.Archivo).ToDictionary(g => g.Key, g => g.Count());

        actuales.Should().BeEquivalentTo(Revisados,
            "un sitio nuevo que escribe rutas de árbol tiene que revisarse (que acabe en \"/\") y añadirse a esta lista");
    }
}
