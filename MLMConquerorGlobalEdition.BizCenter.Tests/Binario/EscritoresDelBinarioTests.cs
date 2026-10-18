using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Regla del Plan P (Tarea 8): solo los escritores revisados crean nodos del binario. Cada uno toma
/// DualTeamGate sobre el hueco (o crea raices, que no ocupan hueco). Un INSERT directo (como el lote
/// «rank-climb», que nunca se subio y dejo 44.119 intrusos) o un generador nuevo tiene que pasar por aqui.
/// Se miran los .cs de los proyectos que no son de pruebas (sin Migrations) y los .sql y .ps1 de Scripts/
/// y de la raiz, salvo Scripts/sql/plan-p/ (la reparacion, que mueve nodos y no los crea).
/// En la Tarea 11 (DualTeamNodes) la lista se reduce a un unico fichero.
/// </summary>
public class EscritoresDelBinarioTests
{
    private static readonly Dictionary<string, string> Revisados = new()
    {
        ["MLMConquerorGlobalEdition.Repository/Services/Teams/PlacementService.cs"] = "el motor unico: candado del hueco y raiz del patrocinador",
        ["MLMConquerorGlobalEdition.BizCenter/Features/Placement/PlaceMember/PlaceMemberHandler.cs"] = "colocacion manual: candado del hueco o del arbol",
        ["MLMConquerorGlobalEdition.AdminAPI/Features/Placement/AdminPlaceMember/AdminPlaceMemberHandler.cs"] = "colocacion forzada: candado del hueco o del arbol",
        ["MLMConquerorGlobalEdition.SignupAPI/Features/Placement/Commands/PlaceMember/PlaceMemberHandler.cs"] = "el miembro elige lado: candado del hueco",
        ["MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs"] = "raiz del alta (ParentMemberId nulo, no ocupa hueco)",
        ["MLMConquerorGlobalEdition.Repository/Seeders/RootAmbassadorSeeder.cs"] = "raiz inicial (ParentMemberId nulo, no ocupa hueco)",
    };

    private static readonly Regex EscribeNodoCs = new(
        @"\bDualTeamTree\s*\.\s*(Add|AddAsync|AddRange|AddRangeAsync)\s*\(|\bnew\s+DualTeamEntity\b",
        RegexOptions.Compiled);
    private static readonly Regex InsertaNodoSql = new(
        @"\bINSERT\s+(INTO\s+)?(\[?dbo\]?\s*\.\s*)?\[?DualTeamTree\]?\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Comentario = new(@"//.*$", RegexOptions.Multiline | RegexOptions.Compiled);

    [Fact]
    public void Solo_los_escritores_revisados_crean_nodos_del_binario()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        var sep  = Path.DirectorySeparatorChar;
        var hallazgos = new List<string>();

        foreach (var proyecto in Directory.GetDirectories(raiz, "MLMConquerorGlobalEdition.*")
                     .Where(d => !d.EndsWith(".Tests", StringComparison.Ordinal)))
        foreach (var archivo in Directory.EnumerateFiles(proyecto, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{sep}obj{sep}") || archivo.Contains($"{sep}bin{sep}")
                || archivo.Contains($"{sep}Migrations{sep}"))
                continue;
            var relativo = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            if (Revisados.ContainsKey(relativo)) continue;
            var texto = Comentario.Replace(File.ReadAllText(archivo), string.Empty);
            if (EscribeNodoCs.IsMatch(texto) || InsertaNodoSql.IsMatch(texto))
                hallazgos.Add(relativo);
        }

        var scripts = Directory.EnumerateFiles(raiz, "*.*", SearchOption.TopDirectoryOnly)
            .Concat(Directory.Exists(Path.Combine(raiz, "Scripts"))
                ? Directory.EnumerateFiles(Path.Combine(raiz, "Scripts"), "*.*", SearchOption.AllDirectories)
                : Enumerable.Empty<string>())
            .Where(f => f.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase));
        foreach (var archivo in scripts)
        {
            var relativo = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            if (relativo.StartsWith("Scripts/sql/plan-p/", StringComparison.OrdinalIgnoreCase)) continue;
            if (InsertaNodoSql.IsMatch(File.ReadAllText(archivo)))
                hallazgos.Add(relativo);
        }

        hallazgos.Should().BeEmpty("un escritor nuevo del binario tiene que tomar DualTeamGate y anadirse a la lista revisada");
    }

    [Fact]
    public void Los_revisados_siguen_existiendo()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        Revisados.Keys.Where(r => !File.Exists(Path.Combine(raiz, r))).Should().BeEmpty("la lista no debe guardar exenciones muertas");
    }
}
