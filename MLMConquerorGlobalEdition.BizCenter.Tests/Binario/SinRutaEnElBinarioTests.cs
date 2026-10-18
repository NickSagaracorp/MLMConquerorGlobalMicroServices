using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Regla del Plan P (Tarea 17, revision C1): ninguna consulta del binario filtra por la ruta de texto
/// (HierarchyPath.StartsWith/Contains/IndexOf/EndsWith, EF.Functions.Like o LIKE sobre HierarchyPath, o un prefijo
/// de ruta con StartsWith). El equipo es el rango de la clave compacta (DualSubtree); con rutas de mas de 4.000
/// caracteres el LIKE falla con el error 8152, y tras la reparacion casi todas lo son.
/// Se mira por sentencia (hasta el ';'), sin comentarios, solo si la sentencia nombra DualTeamTree o
/// DualTeamEntity, en los .cs de los proyectos que no son de pruebas y en los .sql de Scripts/ y la raiz.
/// Leer la ruta para trocearla (la lista de ascendientes) no es un filtro y no entra.
/// </summary>
public class SinRutaEnElBinarioTests
{
    private static readonly Regex RutaEnFiltro = new(
        @"\bHierarchyPath\s*\.\s*(StartsWith|Contains|IndexOf|EndsWith)\s*\(|EF\s*\.\s*Functions\s*\.\s*Like\s*\(\s*\w+\s*\.\s*HierarchyPath|HierarchyPath\s+LIKE\b|\b(path|ruta|prefix|pathPrefix|rutaObjetivo|dualPath|floatPrefix|oldPrefix)\s*\.\s*StartsWith\s*\(",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Comentario = new(@"//.*$|/\*.*?\*/", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ComentarioSql = new(@"--.*$|/\*.*?\*/", RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex NombraElBinario = new(@"\bDualTeam(Tree|Entity)\b", RegexOptions.Compiled);

    /// <summary>Excepciones con nombre y motivo.</summary>
    private static readonly Dictionary<string, string> Excepciones = new()
    {
        ["MLMConquerorGlobalEdition.Repository/Queries/DualTeamNodes.cs"] = "escribe la ruta de texto (no filtra por ella)",
    };

    [Fact]
    public void Ninguna_consulta_del_binario_filtra_por_la_ruta()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        var sep  = Path.DirectorySeparatorChar;
        var hallazgos = new List<string>();

        foreach (var proyecto in Directory.GetDirectories(raiz, "MLMConquerorGlobalEdition.*")
                     .Where(d => !d.EndsWith(".Tests", StringComparison.Ordinal)))
        foreach (var archivo in Directory.EnumerateFiles(proyecto, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{sep}obj{sep}") || archivo.Contains($"{sep}bin{sep}") || archivo.Contains($"{sep}Migrations{sep}"))
                continue;
            var relativo = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            if (Excepciones.ContainsKey(relativo)) continue;
            foreach (var sentencia in Comentario.Replace(File.ReadAllText(archivo), string.Empty).Split(';'))
                if (NombraElBinario.IsMatch(sentencia) && RutaEnFiltro.IsMatch(sentencia))
                    hallazgos.Add($"{relativo}: {RutaEnFiltro.Match(sentencia).Value}");
        }

        var scripts = Directory.EnumerateFiles(raiz, "*.sql", SearchOption.TopDirectoryOnly)
            .Concat(Directory.Exists(Path.Combine(raiz, "Scripts"))
                ? Directory.EnumerateFiles(Path.Combine(raiz, "Scripts"), "*.sql", SearchOption.AllDirectories)
                : Enumerable.Empty<string>());
        foreach (var archivo in scripts)
        {
            var relativo = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            foreach (var sentencia in ComentarioSql.Replace(File.ReadAllText(archivo), string.Empty).Split(';'))
                if (NombraElBinario.IsMatch(sentencia) && RutaEnFiltro.IsMatch(sentencia))
                    hallazgos.Add($"{relativo}: {RutaEnFiltro.Match(sentencia).Value}");
        }

        hallazgos.Should().BeEmpty("el binario se acota con la clave compacta (DualSubtree), no con un prefijo de HierarchyPath");
    }

    [Theory]
    [InlineData("db.DualTeamTree.Where(d => d.HierarchyPath.StartsWith(prefijo))")]
    [InlineData("db.DualTeamTree.Where(d => EF.Functions.Like(d.HierarchyPath, x))")]
    [InlineData("SELECT * FROM DualTeamTree WHERE HierarchyPath LIKE @p")]
    [InlineData("from d in _db.DualTeamTree where pathPrefix.StartsWith(d.HierarchyPath) select d")]
    public void Detecta_cada_forma_de_filtrar_por_la_ruta(string codigo) =>
        (NombraElBinario.IsMatch(codigo) && RutaEnFiltro.IsMatch(codigo)).Should().BeTrue();
}
