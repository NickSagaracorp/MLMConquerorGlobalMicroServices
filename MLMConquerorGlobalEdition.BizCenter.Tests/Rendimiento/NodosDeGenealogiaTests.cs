using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Regla del Plan M (R2), hermana de RutasConBarraFinalTests: quien crea un nodo del árbol de
/// inscripción le asigna Node, los sitios que lo hacen son los revisados, y nadie reescribe
/// GenealogyTree en bloque (un ExecuteUpdate o un UPDATE en SQL dejarían Node incoherente con la
/// estructura). Un sitio nuevo hace fallar la prueba: hay que revisarlo (GenealogyNodes) y añadirlo.
/// </summary>
public class NodosDeGenealogiaTests
{
    private static readonly Regex Alta =
        new(@"new\s+GenealogyEntity\s*\{(?<cuerpo>.*?)\};", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex AsignaNode = new(@"\bNode\s*=(?!=)", RegexOptions.Compiled);
    private static readonly Regex EscrituraEnBloque =
        new(@"ExecuteUpdate|ExecuteDelete|UPDATE\s+\[?GenealogyTree", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Archivo (relativo, con "/") → cuántos nodos de inscripción crea.</summary>
    private static readonly Dictionary<string, int> Revisados = new()
    {
        ["MLMConquerorGlobalEdition.Repository/Seeders/RootAmbassadorSeeder.cs"]                                     = 1,
        ["MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs"] = 1,
        ["MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupMember/SignupMemberHandler.cs"]         = 1,
    };

    private static IEnumerable<(string Archivo, string Texto)> Fuentes()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        var sep  = Path.DirectorySeparatorChar;
        foreach (var proyecto in Directory.GetDirectories(raiz, "MLMConquerorGlobalEdition.*")
                     .Where(d => !d.EndsWith(".Tests", StringComparison.Ordinal)))
        foreach (var archivo in Directory.EnumerateFiles(proyecto, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{sep}obj{sep}") || archivo.Contains($"{sep}bin{sep}")
                || archivo.Contains($"{sep}Migrations{sep}"))
                continue;
            yield return (Path.GetRelativePath(raiz, archivo).Replace('\\', '/'), File.ReadAllText(archivo));
        }
    }

    [Fact]
    public void Todo_nodo_de_inscripcion_nuevo_lleva_Node()
    {
        var sinNode = Fuentes()
            .SelectMany(f => Alta.Matches(f.Texto).Select(m => (f.Archivo, Cuerpo: m.Groups["cuerpo"].Value)))
            .Where(a => !AsignaNode.IsMatch(a.Cuerpo))
            .Select(a => a.Archivo)
            .ToList();

        sinNode.Should().BeEmpty("un nodo sin Node no aparece en ningún equipo (Plan M, R2)");
    }

    [Fact]
    public void Los_sitios_que_crean_nodos_de_inscripcion_son_los_revisados()
    {
        var actuales = Fuentes()
            .Select(f => (f.Archivo, Altas: Alta.Matches(f.Texto).Count))
            .Where(f => f.Altas > 0)
            .ToDictionary(f => f.Archivo, f => f.Altas);

        actuales.Should().BeEquivalentTo(Revisados,
            "un sitio nuevo que crea nodos de inscripción tiene que calcular Node con GenealogyNodes y añadirse aquí");
    }

    [Fact]
    public void Nadie_reescribe_GenealogyTree_en_bloque()
    {
        var hallazgos = Fuentes()
            .SelectMany(f => f.Texto.Split(';').Select(s => (f.Archivo, Sentencia: s)))
            .Where(s => s.Sentencia.Contains("GenealogyTree", StringComparison.Ordinal)
                     && EscrituraEnBloque.IsMatch(s.Sentencia))
            .Select(s => s.Archivo)
            .ToList();

        hallazgos.Should().BeEmpty("mover nodos exige GetReparentedValue sobre todo el subárbol (Plan M, R3)");
    }
}
