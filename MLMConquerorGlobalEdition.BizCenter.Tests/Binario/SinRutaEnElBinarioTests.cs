using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Regla del Plan P (Tarea 17, revision C1), ampliada por el Plan Q (Tarea 14): ninguna sentencia usa la ruta de
/// texto del binario. El binario no tiene ruta: la posicion es la clave compacta (BinaryPath, Depth) y el equipo es
/// su rango (DualSubtree). Cuenta cualquier uso (filtro, lectura o escritura) de HierarchyPath con un alias ligado a
/// DualTeamTree o DualTeamEntity, o sin alias en una sentencia que nombra el binario y no la genealogia (revision I10:
/// la ruta de INSCRIPCION en una sentencia que tambien nombra el binario no es un fallo).
/// Se mira por sentencia (hasta el ';'), sin comentarios, en los .cs de los proyectos que no son de pruebas (sin
/// Migrations), en los .sql de la raiz, en los .sql, .ps1 y .cs de Scripts/ y en los .sql y .ps1 de DeploymentScripts/.
/// </summary>
public class SinRutaEnElBinarioTests
{
    private static readonly Regex Comentario = new(@"//[^\r\n]*|/\*[\s\S]*?\*/", RegexOptions.Compiled);   // revision I2: sin Singleline (se comia el resto del fichero)
    private static readonly Regex ComentarioSql = new(@"--[^\r\n]*|/\*[\s\S]*?\*/", RegexOptions.Compiled);   // revision I2: sin Singleline
    private static readonly Regex NombraElBinario = new(@"\bDualTeam(Tree|Entity)\b", RegexOptions.Compiled);
    private static readonly Regex NombraLaGenealogia = new(@"\bGenealogy(Tree|Entity)\b", RegexOptions.Compiled);

    // Plan Q (revision I10): los alias ligados al binario en la sentencia. SQL: "DualTeamTree d", "DualTeamTree AS d",
    // "[DualTeamTree] d". LINQ: "from d in _db.DualTeamTree" y "DualTeamTree.Where(d => ...)".
    private static readonly Regex AliasDelBinario = new(
        @"\bDualTeam(?:Tree|Entity)\b\]?\s+(?:AS\s+)?(?!(?:WHERE|JOIN|ON|SET|WITH|INNER|LEFT|RIGHT|CROSS|OUTER|ORDER|GROUP|UNION|SELECT|AND|OR|VALUES)\b)(?<a>[A-Za-z_]\w*)" +
        @"|\bfrom\s+(?<a>[A-Za-z_]\w*)\s+in\s+[\w.]*\bDualTeam(?:Tree|Entity)\b" +
        @"|\bDualTeam(?:Tree|Entity)\b[^;]*?\(\s*(?<a>[A-Za-z_]\w*)\s*=>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RutaCualificada = new(@"\b(?<q>[A-Za-z_]\w*)\]?\s*\.\s*\[?HierarchyPath\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RutaSinAlias = new(@"(?<!\.\s*\[?)\bHierarchyPath\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Excepciones con nombre y motivo.</summary>
    private static readonly Dictionary<string, string> Excepciones = new()
    {
        ["DeploymentScripts/migrate-rds.sql"] = "es el historial de migraciones regenerado como script (Tarea 31): crea la columna y la quita en RetiraRutaDelBinario, como Migrations/",
        ["Scripts/sql/plan-p/01-reparar-huecos-binario.sql"] = "retirado por su guarda (51104): no se ejecuta sin la columna",
        ["Scripts/sql/plan-p/escala/altas/g-binario.sql"] = "lo cambia la Tarea 32 (generador de escala)",
        ["Scripts/sql/plan-p/escala/02-verificar-escala.sql"] = "lo cambia la Tarea 32 (verificacion de escala)",
        ["Scripts/sql/plan-q/03-recuperar-espacio-de-la-ruta.sql"] =
            "solo comprueba que la columna ya no existe (COL_LENGTH, 51103) antes de liberar su espacio",
    };

    /// <summary>True si la sentencia (ya sin comentarios) usa la ruta de texto DEL BINARIO.</summary>
    internal static bool UsaLaRutaDelBinario(string sentencia)
    {
        if (!NombraElBinario.IsMatch(sentencia)) return false;
        var alias = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DualTeamTree", "DualTeamEntity" };
        foreach (System.Text.RegularExpressions.Match m in AliasDelBinario.Matches(sentencia))
            if (m.Groups["a"].Success) alias.Add(m.Groups["a"].Value);
        if (RutaCualificada.Matches(sentencia).Any(m => alias.Contains(m.Groups["q"].Value))) return true;
        return RutaSinAlias.IsMatch(sentencia) && !NombraLaGenealogia.IsMatch(sentencia);
    }

    [Fact]
    public void Ninguna_sentencia_usa_la_ruta_del_binario()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        var sep  = Path.DirectorySeparatorChar;
        var hallazgos = new List<string>();

        void Revisar(string archivo, Regex comentarios)
        {
            var relativo = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            if (Excepciones.ContainsKey(relativo)) return;
            foreach (var sentencia in comentarios.Replace(File.ReadAllText(archivo), string.Empty).Split(';'))
                if (UsaLaRutaDelBinario(sentencia))
                    hallazgos.Add($"{relativo}: {sentencia.Trim()[..Math.Min(120, sentencia.Trim().Length)]}");
        }

        foreach (var proyecto in Directory.GetDirectories(raiz, "MLMConquerorGlobalEdition.*")
                     .Where(d => !d.EndsWith(".Tests", StringComparison.Ordinal)))
        foreach (var archivo in Directory.EnumerateFiles(proyecto, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{sep}obj{sep}") || archivo.Contains($"{sep}bin{sep}") || archivo.Contains($"{sep}Migrations{sep}"))
                continue;
            Revisar(archivo, Comentario);
        }

        IEnumerable<string> Ficheros(string carpeta, params string[] patrones) =>
            Directory.Exists(Path.Combine(raiz, carpeta))
                ? patrones.SelectMany(p => Directory.EnumerateFiles(Path.Combine(raiz, carpeta), p, SearchOption.AllDirectories))
                          .Where(f => !f.Contains($"{sep}obj{sep}") && !f.Contains($"{sep}bin{sep}"))
                : Enumerable.Empty<string>();

        foreach (var archivo in Directory.EnumerateFiles(raiz, "*.sql", SearchOption.TopDirectoryOnly)
                     .Concat(Ficheros("Scripts", "*.sql", "*.ps1"))
                     .Concat(Ficheros("DeploymentScripts", "*.sql", "*.ps1")))
            Revisar(archivo, ComentarioSql);
        foreach (var archivo in Ficheros("Scripts", "*.cs"))
            Revisar(archivo, Comentario);

        hallazgos.Should().BeEmpty("el binario no tiene ruta de texto: la posicion es la clave compacta (BinaryPath, Depth)");
    }

    [Theory]
    [InlineData("db.DualTeamTree.Where(d => d.HierarchyPath.StartsWith(prefijo))")]
    [InlineData("db.DualTeamTree.Where(d => EF.Functions.Like(d.HierarchyPath, x))")]
    [InlineData("SELECT * FROM DualTeamTree WHERE HierarchyPath LIKE @p")]
    [InlineData("SELECT d.MemberId, d.HierarchyPath FROM dbo.DualTeamTree d")]
    [InlineData("SELECT x.HierarchyPath FROM [DualTeamTree] AS x JOIN GenealogyTree g ON g.MemberId = x.MemberId")]
    [InlineData("from d in _db.DualTeamTree where pathPrefix.StartsWith(d.HierarchyPath) select d")]
    public void Detecta_la_ruta_del_binario(string codigo) => UsaLaRutaDelBinario(codigo).Should().BeTrue();

    /// <summary>Revision I10: la ruta de INSCRIPCION en una sentencia que tambien nombra el binario no es un fallo.</summary>
    [Theory]
    [InlineData(@"SELECT m.MemberId FROM dbo.MemberProfiles m
JOIN dbo.GenealogyTree g ON g.MemberId = m.MemberId AND g.IsDeleted = 0
WHERE (g.HierarchyPath LIKE '%/AMB-700829/%' OR m.SponsorMemberId='AMB-700829')
  AND m.MemberType = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.DualTeamTree d WHERE d.MemberId = m.MemberId)")]   // backfill-placements.ps1:104, backfill-left.ps1:89
    [InlineData("SELECT g.HierarchyPath FROM GenealogyTree g WHERE EXISTS (SELECT 1 FROM DualTeamTree WHERE MemberId = g.MemberId)")]
    public void No_confunde_la_ruta_de_inscripcion(string codigo) => UsaLaRutaDelBinario(codigo).Should().BeFalse();

    /// <summary>Revision I2: un uso de la ruta puesto despues de un comentario tambien se ve.</summary>
    [Fact]
    public void Detecta_un_uso_puesto_despues_de_un_comentario()
    {
        var sql = @"-- un comentario
SELECT 1;
SELECT * FROM DualTeamTree WHERE HierarchyPath LIKE @p";
        ComentarioSql.Replace(sql, string.Empty).Split(';').Any(UsaLaRutaDelBinario).Should().BeTrue();
    }
}
