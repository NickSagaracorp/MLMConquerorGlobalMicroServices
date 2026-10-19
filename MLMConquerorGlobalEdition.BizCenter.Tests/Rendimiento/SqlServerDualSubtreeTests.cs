using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan P (Tareas 13 y 15): el equipo binario es el rango de la clave compacta (DualSubtree), con arboles propios
/// dentro de una transaccion. Sustituye a las pruebas de la CTE de 20.000 del Plan M, que se retiro.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerDualSubtreeTests
{
    private readonly ITestOutputHelper _salida;

    public SqlServerDualSubtreeTests(ITestOutputHelper salida) => _salida = salida;

    [SqlServerFact]
    public Task Under_y_Leg_devuelven_el_subarbol_y_cada_pierna() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = SqlServerReportes.NuevoId(); var a = SqlServerReportes.NuevoId(); var b = SqlServerReportes.NuevoId(); var c = SqlServerReportes.NuevoId();
        var raiz = ReportesDb.NodoBinario(db, r, null, ReportesDb.Ruta(r), TreeSide.Left);
        ReportesDb.NodoBinario(db, a, r, ReportesDb.Ruta(r, a), TreeSide.Left);
        ReportesDb.NodoBinario(db, b, r, ReportesDb.Ruta(r, b), TreeSide.Right);
        ReportesDb.NodoBinario(db, c, a, ReportesDb.Ruta(r, a, c), TreeSide.Right);
        await db.SaveChangesAsync();

        (await DualSubtree.Under(db, raiz).Select(d => d.MemberId).ToListAsync()).Should().BeEquivalentTo(a, b, c);
        (await DualSubtree.Leg(db, raiz, TreeSide.Left).Select(d => d.MemberId).ToListAsync()).Should().BeEquivalentTo(a, c);
        (await DualSubtree.Leg(db, raiz, TreeSide.Right).Select(d => d.MemberId).ToListAsync()).Should().BeEquivalentTo(b);
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public Task Mi_equipo_por_la_clave_da_nivel_y_pierna() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var r = SqlServerReportes.NuevoId(); var a = SqlServerReportes.NuevoId(); var b = SqlServerReportes.NuevoId(); var c = SqlServerReportes.NuevoId();
        foreach (var id in new[] { r, a, b, c }) ReportesDb.Miembro(db, id);
        ReportesDb.NodoBinario(db, r, null, ReportesDb.Ruta(r), TreeSide.Left);
        ReportesDb.NodoBinario(db, a, r, ReportesDb.Ruta(r, a), TreeSide.Left);
        ReportesDb.NodoBinario(db, b, a, ReportesDb.Ruta(r, a, b), TreeSide.Right);
        ReportesDb.NodoBinario(db, c, r, ReportesDb.Ruta(r, c), TreeSide.Right);
        await db.SaveChangesAsync();
        var servicio = new DualTeamService(db, Reportes.CalificadoEnLosEquiposTests.Rangos(db), Reportes.CalificadoEnLosEquiposTests.Calificacion(db));

        var filas = (await servicio.GetMyTeamGridAsync(r, new GridDataRequest { PageSize = 50 })).Items.ToList();

        filas.Select(f => (f.MemberId, f.Level, f.Leg)).Should().BeEquivalentTo(new[]
        {
            (a, 1, "Left"), (b, 2, "Left"), (c, 1, "Right")
        });
        await tx.RollbackAsync();
    });

    /// <summary>
    /// Revision C1: con una ruta de mas de 4.000 caracteres, el filtro por prefijo de HierarchyPath falla en SQL
    /// Server (lo que hacian Mi equipo por encima de la CTE, la busqueda, el nodo mas profundo, el listado del admin,
    /// el progreso de rango y el visualizador). Tras la reparacion casi todos los nodos estan asi. Por la clave
    /// compacta no falla: se comprueban la busqueda y el nodo mas profundo, que antes filtraban por la ruta.
    /// </summary>
    [SqlServerFact]
    public Task Con_una_ruta_de_mas_de_4000_caracteres_el_prefijo_falla_y_la_clave_no() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var ids = Enumerable.Range(0, 300).Select(_ => SqlServerReportes.NuevoId()).ToList();
        var ruta = string.Empty;
        for (var i = 0; i < ids.Count; i++)
        {
            ruta = i == 0 ? $"/{ids[0]}/" : ruta + ids[i] + "/";
            ReportesDb.NodoBinario(db, ids[i], i == 0 ? null : ids[i - 1], ruta, TreeSide.Left);
        }
        foreach (var id in ids) ReportesDb.Miembro(db, id);
        await db.SaveChangesAsync();
        var medio = ids[290];
        var rutaMedio = await db.DualTeamTree.Where(d => d.MemberId == medio).Select(d => d.HierarchyPath).SingleAsync();
        rutaMedio.Length.Should().BeGreaterThan(4000);

        // Hoy (el filtro por prefijo): falla.
        var prefijo = () => db.DualTeamTree.AsNoTracking().CountAsync(d => d.HierarchyPath.StartsWith(rutaMedio));
        (await prefijo.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(8152);

        // Por la clave: funciona y da lo que tiene que dar.
        var servicio = new DualTeamService(db, Reportes.CalificadoEnLosEquiposTests.Rangos(db), Reportes.CalificadoEnLosEquiposTests.Calificacion(db));
        var nodoMedio = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == medio);
        (await DualSubtree.Under(db, nodoMedio).CountAsync()).Should().Be(9);
        var hondo = await servicio.GetDeepestNodeAsync(medio, TreeSide.Left);
        hondo!.MemberId.Should().Be(ids[^1]);
        var encontrados = await servicio.SearchBinarySubtreeAsync(medio, ids[^1]);
        encontrados.Select(e => e.MemberId).Should().Equal(ids[^1]);
        await tx.RollbackAsync();
    });

    /// <summary>El rango es un Index Seek sobre UX_DualTeamTree_BinaryPath, con la etiqueta de recompilacion.</summary>
    [SqlServerFact]
    public Task El_equipo_es_un_seek_sobre_la_clave_y_se_recompila() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        var raiz = await MiembroConMayorEquipoAsync(db);
        var nodo = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == raiz.MemberId);
        var consulta = DualSubtree.Under(db, nodo).AsNoTracking().Select(d => d.MemberId);

        consulta.ToQueryString().Should().Contain(DualSubtree.RecompileTag);
        var plan = await PlanDeEjecucion.CompiladoAsync(db, consulta);
        plan.Should().Contain("UX_DualTeamTree_BinaryPath").And.Contain(PlanDeEjecucion.Seek);
    });

    /// <summary>
    /// Plan Q (revision del Bloque 1): los calificados del equipo binario leen el rango SOLO por el indice cubriente
    /// UX_DualTeamTree_BinaryPath (lleva MemberId incluido). Con SELECT * el optimizador elegia a veces una interseccion
    /// paralela de tres indices (400 a 1.660 ms en local con 119.000); por el indice cubriente, unos 200 ms estables.
    /// El conteo es el mismo que por el rango completo.
    /// </summary>
    [SqlServerFact]
    public Task Los_calificados_del_equipo_leen_el_rango_por_el_indice_cubriente() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        var raiz = await MiembroConMayorEquipoAsync(db);
        var nodo = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == raiz.MemberId);
        var activo = Domain.Entities.Membership.MembershipStatus.Active;
        var consulta =
            from id in DualSubtree.MemberIdsUnder(db, nodo)
            join s in db.MembershipSubscriptions.AsNoTracking() on id equals s.MemberId
            where s.SubscriptionStatus == activo
            select s.Id;

        consulta.ToQueryString().Should().Contain(DualSubtree.RecompileTag);
        var plan = await PlanDeEjecucion.CompiladoAsync(db, consulta);
        var indices = System.Xml.Linq.XDocument.Parse(plan).Descendants()
            .Where(e => e.Name.LocalName == "Object" && (string?)e.Attribute("Table") == "[DualTeamTree]")
            .Select(e => (string?)e.Attribute("Index"))
            .ToList();
        indices.Should().NotBeEmpty().And.OnlyContain(i => i == "[UX_DualTeamTree_BinaryPath]",
            "el rango se lee por el indice cubriente, sin interseccion con otros indices ni lectura de la tabla");
        plan.Should().Contain(PlanDeEjecucion.Seek);

        var porElRango = await (
            from s in db.MembershipSubscriptions.AsNoTracking()
            join d in DualSubtree.Under(db, nodo).AsNoTracking() on s.MemberId equals d.MemberId
            where s.SubscriptionStatus == activo
            select s.Id).CountAsync();
        (await consulta.CountAsync()).Should().Be(porElRango);
    });

    /// <summary>
    /// El miembro con el equipo binario mas grande (en local, la raiz AMB-700829, unas 119.400 personas), por el
    /// rango de cada raiz. Lo usan las mediciones de Mi equipo y del progreso de rango.
    /// </summary>
    internal static async Task<(string MemberId, int Nodos)> MiembroConMayorEquipoAsync(AppDbContext db)
    {
        var raices = await db.DualTeamTree.AsNoTracking().Where(d => d.ParentMemberId == null).ToListAsync();
        var equipos = new List<(string MemberId, int Nodos)>();
        foreach (var r in raices)
            equipos.Add((r.MemberId, await DualSubtree.Under(db, r).CountAsync()));
        return equipos.OrderByDescending(e => e.Nodos).ThenBy(e => e.MemberId, StringComparer.Ordinal).First();
    }

    [SqlServerFact]
    public Task Mi_equipo_del_mayor_equipo_cabe_en_tiempo() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var (miembro, nodos) = await MiembroConMayorEquipoAsync(db);
        var servicio = new DualTeamService(db, Reportes.CalificadoEnLosEquiposTests.Rangos(db), Reportes.CalificadoEnLosEquiposTests.Calificacion(db));

        await servicio.GetMyTeamGridAsync(miembro, new GridDataRequest { PageSize = 20 });   // calienta
        var cronometro = Stopwatch.StartNew();
        var pagina = await servicio.GetMyTeamGridAsync(miembro, new GridDataRequest { PageSize = 20 });
        cronometro.Stop();

        _salida.WriteLine($"Mi equipo Dual de {miembro}: {nodos} nodos, {pagina.TotalCount} filas en {cronometro.ElapsedMilliseconds} ms");
        cronometro.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
        await tx.RollbackAsync();
    });
}
