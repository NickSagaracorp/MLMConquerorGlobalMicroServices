using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Regla del Plan P (Tarea 8, ampliada por la revision I6): solo los escritores revisados ESCRIBEN en el binario
/// (DualTeamTree), por cualquier camino. Un INSERT directo (como el lote «rank-climb», que nunca se subio y dejo
/// 44.119 intrusos), un UPDATE de rutas o un generador nuevo tiene que pasar por aqui.
/// Se busca, sin comentarios:
/// <list type="bullet">
/// <item>EF: DualTeamTree.Add/AddRange/Update/UpdateRange/Remove/RemoveRange/Attach/AttachRange,
/// ExecuteUpdate/ExecuteDelete sobre DualTeamTree, Set&lt;DualTeamEntity&gt;() y new DualTeamEntity;</item>
/// <item>asignaciones a ParentMemberId, Side, HierarchyPath, BinaryPath o Depth en ficheros que nombran el binario;</item>
/// <item>SQL: INSERT, UPDATE, DELETE o MERGE que nombren DualTeamTree, y SqlBulkCopy hacia DualTeamTree.</item>
/// </list>
/// En los .cs de los proyectos que no son de pruebas (sin Migrations) y en los .cs, .sql y .ps1 de Scripts/,
/// DeploymentScripts/ y la raiz.
/// </summary>
public class EscritoresDelBinarioTests
{
    private static readonly Dictionary<string, string> Revisados = new()
    {
        ["MLMConquerorGlobalEdition.Repository/Queries/DualTeamNodes.cs"] =
            "el unico que construye nodos y reescribe posicion y claves (altas, raices, movimientos y retiradas)",
        ["MLMConquerorGlobalEdition.Repository/Services/Teams/LegPointMaintenance.cs"] =
            "solo puntos y contadores de pierna (UPDATE de LeftLeg*/RightLeg*), no la estructura",
        ["MLMConquerorGlobalEdition.Repository/Services/Teams/PlacementService.cs"] =
            "anade al contexto los nodos que construye DualTeamNodes, con el candado del hueco",
        ["MLMConquerorGlobalEdition.BizCenter/Features/Placement/PlaceMember/PlaceMemberHandler.cs"] =
            "anade la hoja que construye DualTeamNodes, con el candado del hueco",
        ["MLMConquerorGlobalEdition.AdminAPI/Features/Placement/AdminPlaceMember/AdminPlaceMemberHandler.cs"] =
            "anade la hoja o la raiz que construye DualTeamNodes, con el candado del hueco o del arbol",
        ["MLMConquerorGlobalEdition.SignupAPI/Features/Placement/Commands/PlaceMember/PlaceMemberHandler.cs"] =
            "anade la hoja o la raiz que construye DualTeamNodes, con el candado del hueco",
        ["MLMConquerorGlobalEdition.SignupAPI/Features/Signups/Commands/SignupAmbassador/SignupAmbassadorHandler.cs"] =
            "anade la raiz del alta que construye DualTeamNodes (no ocupa hueco)",
        ["MLMConquerorGlobalEdition.Repository/Seeders/RootAmbassadorSeeder.cs"] =
            "anade la raiz inicial que construye DualTeamNodes (no ocupa hueco)",
        ["Scripts/sql/plan-p/01-reparar-huecos-binario.sql"] = "reparacion local con cerrojos y candado dual-tree",
        ["Scripts/sql/plan-p/02-recalcular-piernas.sql"] = "piernas desde cero, con cerrojos y candado dual-tree",
        ["Scripts/sql/plan-p/03-relleno-clave-binario.sql"] = "relleno de la clave, con cerrojos y candado dual-tree",
        ["Scripts/sql/plan-p/04-contadores-y-colas.sql"] = "contadores de pierna, con cerrojos y candado dual-tree",
    };

    private static readonly Regex EscrituraEf = new(
        @"\bDualTeamTree\s*\.\s*(Add|AddAsync|AddRange|AddRangeAsync|Update|UpdateRange|Remove|RemoveRange|Attach|AttachRange)\s*\(" +
        @"|\bDualTeamTree\b[^;]*?\.\s*(ExecuteUpdate|ExecuteUpdateAsync|ExecuteDelete|ExecuteDeleteAsync)\s*\(" +
        @"|\bSet\s*<\s*DualTeamEntity\s*>\s*\(" +
        @"|\bnew\s+DualTeamEntity\b",
        RegexOptions.Compiled);
    private static readonly Regex Asignacion = new(
        @"\.\s*(ParentMemberId|Side|HierarchyPath|BinaryPath|Depth)\s*=(?!=)", RegexOptions.Compiled);
    private static readonly Regex NombraElBinario = new(@"\bDualTeam(Tree|Entity)\b", RegexOptions.Compiled);
    private static readonly Regex EscrituraSql = new(
        @"\b(INSERT|UPDATE|DELETE|MERGE)\b[^;]{0,600}?\bDualTeamTree\b|DestinationTableName\s*=\s*""[^""]*DualTeamTree",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ComentarioCs = new(@"//.*$|/\*.*?\*/", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ComentarioSql = new(@"--.*$|/\*.*?\*/", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);

    private static IEnumerable<string> Ficheros(string raiz)
    {
        var sep = Path.DirectorySeparatorChar;
        foreach (var proyecto in Directory.GetDirectories(raiz, "MLMConquerorGlobalEdition.*")
                     .Where(d => !d.EndsWith(".Tests", StringComparison.Ordinal)))
        foreach (var f in Directory.EnumerateFiles(proyecto, "*.cs", SearchOption.AllDirectories))
            if (!f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}") && !f.Contains($"{sep}Migrations{sep}"))
                yield return f;

        foreach (var carpeta in new[] { "Scripts", "DeploymentScripts" }.Select(c => Path.Combine(raiz, c)).Where(Directory.Exists))
        foreach (var f in Directory.EnumerateFiles(carpeta, "*.*", SearchOption.AllDirectories))
            if (EsScript(f) && !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
                yield return f;

        foreach (var f in Directory.EnumerateFiles(raiz, "*.*", SearchOption.TopDirectoryOnly).Where(EsScript))
            yield return f;
    }

    private static bool EsScript(string f) =>
        f.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
        || f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Solo_los_escritores_revisados_escriben_en_el_binario()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        var hallazgos = new List<string>();
        foreach (var archivo in Ficheros(raiz))
        {
            var relativo = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            if (Revisados.ContainsKey(relativo)) continue;

            var bruto = File.ReadAllText(archivo);
            var esSql = archivo.EndsWith(".sql", StringComparison.OrdinalIgnoreCase);
            var texto = esSql ? ComentarioSql.Replace(bruto, string.Empty) : ComentarioCs.Replace(bruto, string.Empty);

            var m = EscrituraSql.Match(texto);
            if (m.Success) { hallazgos.Add($"{relativo}: {Corto(m.Value)}"); continue; }
            if (esSql) continue;
            m = EscrituraEf.Match(texto);
            if (m.Success) { hallazgos.Add($"{relativo}: {Corto(m.Value)}"); continue; }
            if (NombraElBinario.IsMatch(texto) && (m = Asignacion.Match(texto)).Success)
                hallazgos.Add($"{relativo}: {Corto(m.Value)}");
        }

        hallazgos.Should().BeEmpty("un escritor nuevo del binario tiene que pasar por DualTeamNodes y DualTeamGate y anadirse a la lista revisada");
    }

    [Fact]
    public void Los_revisados_siguen_existiendo()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        Revisados.Keys.Where(r => !File.Exists(Path.Combine(raiz, r))).Should().BeEmpty("la lista no debe guardar exenciones muertas");
    }

    /// <summary>La regla detecta cada forma de escribir el binario (si no, una lista vacia no diria nada).</summary>
    [Theory]
    [InlineData("_db.DualTeamTree.Add(nodo);")]
    [InlineData("db.DualTeamTree.Remove(x);")]
    [InlineData("db.DualTeamTree.Attach(x);")]
    [InlineData("db.DualTeamTree.Where(d => d.IsDeleted).ExecuteUpdate(s => s.SetProperty(d => d.Side, TreeSide.Left));")]
    [InlineData("db.Set<DualTeamEntity>().Add(x);")]
    [InlineData("var n = new DualTeamEntity { MemberId = id };")]
    public void Detecta_las_escrituras_por_EF(string codigo) => EscrituraEf.IsMatch(codigo).Should().BeTrue();

    [Theory]
    [InlineData("INSERT INTO DualTeamTree (MemberId) VALUES ('x')")]
    [InlineData("UPDATE d SET HierarchyPath = N'/x/' FROM DualTeamTree d")]
    [InlineData("update [dbo].[DualTeamTree] set Side = 1")]
    [InlineData("DELETE FROM DualTeamTree WHERE MemberId = 'x'")]
    [InlineData("MERGE DualTeamTree AS t USING s ON t.Id = s.Id")]
    [InlineData("bulk.DestinationTableName = \"dbo.DualTeamTree\";")]
    public void Detecta_las_escrituras_por_SQL(string codigo) => EscrituraSql.IsMatch(codigo).Should().BeTrue();

    [Fact]
    public void Detecta_las_asignaciones_de_posicion_en_un_fichero_del_binario()
    {
        const string codigo = "var n = db.DualTeamTree.First(); n.ParentMemberId = otro;";
        (NombraElBinario.IsMatch(codigo) && Asignacion.IsMatch(codigo)).Should().BeTrue();
    }

    private static string Corto(string s) => s.Length > 120 ? s[..120] + "…" : s;
}
