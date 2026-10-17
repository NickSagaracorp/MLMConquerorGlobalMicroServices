using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Regla del Plan M (M16): ninguna consulta del árbol de INSCRIPCIÓN filtra por la ruta de texto
/// (HierarchyPath.StartsWith/Contains, o una ruta leída del árbol con StartsWith). El equipo se
/// acota con Node (GenealogySubtree). Se mira por sentencia (hasta el ';') que nombre GenealogyTree,
/// sin comentarios. El binario (DualTeamTree) no entra: sigue con su ruta o con DualSubtree.
/// </summary>
public class SinPrefijoEnInscripcionTests
{
    private static readonly Regex Prefijo =
        new(@"\b(HierarchyPath|node|path|ruta)\s*\.\s*(StartsWith|Contains)\s*\(", RegexOptions.Compiled);
    private static readonly Regex Comentario = new(@"//.*$", RegexOptions.Multiline | RegexOptions.Compiled);

    [Fact]
    public void Ninguna_consulta_del_arbol_de_inscripcion_filtra_por_la_ruta()
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
            foreach (var sentencia in Comentario.Replace(File.ReadAllText(archivo), string.Empty).Split(';'))
                if (sentencia.Contains("GenealogyTree", StringComparison.Ordinal) && Prefijo.IsMatch(sentencia))
                    hallazgos.Add($"{relativo}: {Prefijo.Match(sentencia).Value}");
        }

        hallazgos.Should().BeEmpty("el equipo de inscripción se acota con Node.IsDescendantOf (Plan M), no con un LIKE sobre HierarchyPath");
    }
}
