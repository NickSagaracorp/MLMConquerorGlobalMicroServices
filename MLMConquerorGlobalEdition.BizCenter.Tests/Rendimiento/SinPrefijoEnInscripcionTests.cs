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
        // Revisión M-l: sin distinguir mayúsculas, y también IndexOf y EF.Functions.Like sobre la ruta.
        new(@"\b(HierarchyPath|node|path|ruta)\s*\.\s*(StartsWith|Contains)\s*\(|\bHierarchyPath\s*\.\s*IndexOf\s*\(|EF\s*\.\s*Functions\s*\.\s*Like\s*\(\s*\w+\s*\.\s*HierarchyPath",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
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

    private static readonly Regex FilaPorIsDescendantOf =
        new(@"\b[a-z]\d?\s*\.\s*Node!?\s*\.\s*IsDescendantOf\s*\(", RegexOptions.Compiled);

    /// <summary>
    /// Plan M (desviación de la Tarea 13): una fila del árbol (g, o…) no se filtra por equipo con
    /// IsDescendantOf, sino con GenealogySubtree.Under/CurrentUnder. EF manda IsDescendantOf(@p) =
    /// CAST(1 AS bit), que no hace seek y hace que SQL Server estime una fila para cualquier equipo:
    /// 9 s con el de 119.400 personas frente a 46 ms con el rango de Under. La forma A (el Node de un
    /// conjunto pequeño ya filtrado, en una subconsulta: «node.IsDescendantOf(root)») sí puede usarlo.
    /// </summary>
    [Fact]
    public void Ninguna_fila_del_arbol_se_filtra_por_equipo_con_IsDescendantOf()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        var sep  = Path.DirectorySeparatorChar;
        var hallazgos = new List<string>();

        foreach (var proyecto in Directory.GetDirectories(raiz, "MLMConquerorGlobalEdition.*")
                     .Where(d => !d.EndsWith(".Tests", StringComparison.Ordinal)))
        foreach (var archivo in Directory.EnumerateFiles(proyecto, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{sep}obj{sep}") || archivo.Contains($"{sep}bin{sep}")
                || archivo.Contains($"{sep}Migrations{sep}")
                || archivo.EndsWith($"{sep}Queries{sep}GenealogySubtree.cs", StringComparison.Ordinal))
                continue;

            var relativo = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            foreach (System.Text.RegularExpressions.Match m in FilaPorIsDescendantOf.Matches(Comentario.Replace(File.ReadAllText(archivo), string.Empty)))
                hallazgos.Add($"{relativo}: {m.Value}");
        }

        hallazgos.Should().BeEmpty("el equipo se acota con GenealogySubtree.Under (rango del índice), no con IsDescendantOf");
    }

    private static readonly Regex RutaEnMemoria = new(
        @"\b(?<fila>\w+)\s*\.\s*HierarchyPath\s*\.\s*(Contains|StartsWith|IndexOf)\s*\(|\b(path|ruta|node|hierarchyPath)\s*\.\s*(Contains|StartsWith)\s*\(|EF\s*\.\s*Functions\s*\.\s*Like\s*\(\s*(?<fila>\w+)\s*\.\s*HierarchyPath",
        RegexOptions.Compiled | RegexOptions.IgnoreCase); // revisión M-l

    /// <summary>
    /// Revisión M6: también en memoria. En un archivo que lee el árbol de inscripción, ninguna línea
    /// filtra filas por la ruta de texto con Contains/StartsWith, aunque la sentencia no nombre
    /// GenealogyTree (el bono de coche cargaba las filas y filtraba con x.HierarchyPath.Contains). Las
    /// del binario (fila «d» o «dual…») no entran: el binario sigue con su ruta (Bloque 4).
    /// </summary>
    [Fact]
    public void Nadie_filtra_en_memoria_filas_de_genealogia_por_la_ruta()
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

            var texto = Comentario.Replace(File.ReadAllText(archivo), string.Empty);
            if (!texto.Contains("GenealogyTree", StringComparison.Ordinal))
                continue;

            var relativo = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            foreach (var linea in texto.Split('\n'))
            foreach (System.Text.RegularExpressions.Match m in RutaEnMemoria.Matches(linea))
            {
                var fila = m.Groups["fila"].Value;
                if (fila == "d" || fila.StartsWith("dual", StringComparison.OrdinalIgnoreCase)
                    || linea.Contains("Dual", StringComparison.Ordinal) || linea.Contains("dual", StringComparison.Ordinal))
                    continue;
                hallazgos.Add($"{relativo}: {linea.Trim()}");
            }
        }

        hallazgos.Should().BeEmpty("las filas del árbol de inscripción se acotan por Node (GenealogySubtree), también en memoria");
    }

    private static readonly Regex MasReciente =
        new(@"OrderByDescending\s*\(\s*(\w+)\s*=>\s*\1\s*\.\s*CreationDate\s*\)", RegexOptions.Compiled);
    private static readonly Regex DesempatePorId =
        new(@"ThenByDescending\s*\(\s*(\w+)\s*=>\s*\1\s*\.\s*Id\s*\)", RegexOptions.Compiled);

    /// <summary>
    /// Revisión M-e: quien elige el nodo vigente de un miembro (el más reciente) desempata por Id, como
    /// GenealogySubtree.CurrentOfAsync/NodeOfAsync: con dos nodos de la misma fecha, sin desempate cada
    /// consulta podía quedarse con uno distinto. Se mira por sentencia que nombre GenealogyTree.
    /// </summary>
    [Fact]
    public void Quien_elige_el_nodo_mas_reciente_desempata_por_Id()
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
                if (sentencia.Contains("GenealogyTree", StringComparison.Ordinal)
                    && MasReciente.Matches(sentencia).Count > DesempatePorId.Matches(sentencia).Count)
                    hallazgos.Add($"{relativo}: {MasReciente.Match(sentencia).Value}");
        }

        hallazgos.Should().BeEmpty("el nodo vigente se elige con desempate por Id (GenealogySubtree.CurrentOfAsync)");
    }
}
