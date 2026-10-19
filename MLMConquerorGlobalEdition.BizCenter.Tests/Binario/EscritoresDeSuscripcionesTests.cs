using System.Text.RegularExpressions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Regla del Plan Q (DQ13): los calificados del equipo los mueve QualifiedTeamCountsInterceptor al guardar. Un escritor de
/// MembershipSubscriptions por fuera del seguimiento de cambios (ExecuteUpdate/ExecuteDelete, ExecuteSql, SQL crudo con
/// INSERT/UPDATE/DELETE/MERGE o SqlBulkCopy) cambiaria suscripciones activas sin mover los contadores. Se mira, sin
/// comentarios, en los .cs de los proyectos que no son de pruebas (sin Migrations) y en los .cs, .sql y .ps1 de Scripts/,
/// DeploymentScripts/ y la raiz. Complementa a EstadosSinInterceptorTests (Plan J), que mira solo C#.
/// </summary>
public class EscritoresDeSuscripcionesTests
{
    private static readonly Dictionary<string, string> Revisados = new()
    {
        ["Scripts/sql/plan-p/escala/altas/c-pedidos.sql"] =
            "generador de escala (solo MLMConquerorGlobalEditionDb_Escala); despues corren 04-contadores-y-colas.sql y 04-calificados-de-inscripcion.sql",
        ["Scripts/sql/plan-p/escala/02-verificar-escala.sql"] = "solo lectura: el INSERT es a una tabla temporal",
        ["Scripts/sql/plan-p/04-contadores-y-colas.sql"] =
            "solo lee las suscripciones (UPDATE de la temporal #n e INSERT de propios en DualLegDeltas): es el recalculo de los calificados",
    };

    private static readonly Regex EscrituraSql = new(
        @"\b(INSERT|UPDATE|DELETE|MERGE)\b[^;]{0,600}?\bMembershipSubscriptions\b|DestinationTableName\s*=\s*""[^""]*MembershipSubscriptions",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EscrituraEf = new(
        @"\bMembershipSubscriptions\b[^;]*?\.\s*(ExecuteUpdate|ExecuteUpdateAsync|ExecuteDelete|ExecuteDeleteAsync)\s*\(" +
        @"|\bExecuteSql\w*\s*\([^;]*\bMembershipSubscriptions\b",
        RegexOptions.Compiled);
    private static readonly Regex ComentarioCs = new(@"//[^\r\n]*|/\*[\s\S]*?\*/", RegexOptions.Compiled);
    private static readonly Regex ComentarioSql = new(@"--[^\r\n]*|/\*[\s\S]*?\*/", RegexOptions.Compiled);

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
    public void Nadie_escribe_suscripciones_por_fuera_del_interceptor()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        var hallazgos = new List<string>();
        foreach (var archivo in Ficheros(raiz))
        {
            var relativo = Path.GetRelativePath(raiz, archivo).Replace('\\', '/');
            if (Revisados.ContainsKey(relativo)) continue;
            var esSql = archivo.EndsWith(".sql", StringComparison.OrdinalIgnoreCase);
            var texto = (esSql ? ComentarioSql : ComentarioCs).Replace(File.ReadAllText(archivo), string.Empty);
            var m = EscrituraSql.Match(texto);
            if (!m.Success && !esSql) m = EscrituraEf.Match(texto);
            if (m.Success) hallazgos.Add($"{relativo}: {m.Value[..Math.Min(120, m.Value.Length)]}");
        }
        hallazgos.Should().BeEmpty("un cambio de suscripciones fuera de EF no mueve los calificados del equipo (DQ13): " +
                                   "pasar por el seguimiento de cambios o anadirlo a Revisados con su recalculo");
    }

    [Fact]
    public void Los_revisados_siguen_existiendo()
    {
        var raiz = ReportesDb.RaizDelRepositorio();
        Revisados.Keys.Where(r => !File.Exists(Path.Combine(raiz, r))).Should().BeEmpty();
    }

    [Theory]
    [InlineData("UPDATE MembershipSubscriptions SET SubscriptionStatus = 4 WHERE MemberId = @m")]
    [InlineData("update s set SubscriptionStatus = 1 from [dbo].[MembershipSubscriptions] s")]
    [InlineData("INSERT INTO MembershipSubscriptions (MemberId) VALUES ('x')")]
    [InlineData("MERGE MembershipSubscriptions AS t USING s ON t.Id = s.Id")]
    [InlineData("bulk.DestinationTableName = \"dbo.MembershipSubscriptions\";")]
    public void Detecta_las_escrituras_por_SQL(string codigo) => EscrituraSql.IsMatch(codigo).Should().BeTrue();

    [Theory]
    [InlineData("await _db.MembershipSubscriptions.Where(s => s.MemberId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.SubscriptionStatus, MembershipStatus.Cancelled));")]
    [InlineData("await _db.Database.ExecuteSqlRawAsync(\"DELETE FROM MembershipSubscriptions WHERE Id = {0}\", id);")]
    public void Detecta_las_escrituras_masivas_por_EF(string codigo) => EscrituraEf.IsMatch(codigo).Should().BeTrue();
}
