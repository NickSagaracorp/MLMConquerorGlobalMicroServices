using System.Data;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Binario;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan P: mediciones sobre la base reparada (8.223 niveles en local) con la clave compacta. Escrituras a mas de
/// 8.000 niveles (colocar una hoja, mover una raiz provisional con equipo, retirar un nodo) y lecturas del mayor
/// equipo (Mi equipo y la consulta binaria del progreso de rango). Objetivo: menos de 2 s cada una.
/// Las escrituras van en una transaccion que se deshace. Pruebas de tiempo: solo con Medicion.runsettings y contra
/// una base migrada y rellena (MLM_TEST_SQLSERVER).
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
[Trait(SqlServerMedicionPlanMTests.Categoria, SqlServerMedicionPlanMTests.Medicion)]
public class SqlServerMedicionPlanPTests
{
    private static readonly TimeSpan Objetivo = TimeSpan.FromSeconds(2);
    private readonly ITestOutputHelper _salida;

    public SqlServerMedicionPlanPTests(ITestOutputHelper salida) => _salida = salida;

    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private async Task<TimeSpan> MideAsync(string que, Func<Task> paso)
    {
        var c = Stopwatch.StartNew();
        await paso();
        c.Stop();
        _salida.WriteLine($"{que}: {c.ElapsedMilliseconds} ms");
        return c.Elapsed;
    }

    [SqlServerFact]
    public async Task Escrituras_a_mas_de_8000_niveles()
    {
        await using var db = SqlServerReportes.Db();
        var hondo = await db.DualTeamTree.AsNoTracking().OrderByDescending(d => d.Depth).ThenBy(d => d.Id).FirstAsync();
        _salida.WriteLine($"Nodo mas profundo: {hondo.MemberId}, profundidad {hondo.Depth}, clave de {hondo.BinaryPath!.Length} bytes");
        hondo.Depth.Should().BeGreaterThan(8000);

        var x = BinarioDb.Prefijo("MDP");
        await using var tx = await db.Database.BeginTransactionAsync();
        var motor = new PlacementService(db, new Reloj());

        // 1. Una hoja bajo el nodo mas profundo (sube puntos y contadores por mas de 8.000 ascendientes).
        await BinarioDb.SembrarPerfilAsync(db, $"{x}H", hondo.MemberId);
        await BinarioDb.PuntosAsync(db, $"{x}H", 25);
        var t1 = await MideAsync($"Colocar una hoja a profundidad {hondo.Depth + 1}", async () =>
            (await motor.PlaceAsync($"{x}H", hondo.MemberId)).IsSuccess.Should().BeTrue());

        // 2. Una raiz provisional con un equipo de 50 que se coloca bajo la hoja nueva (mueve 51 claves a 8.000 niveles).
        await BinarioDb.SembrarPerfilAsync(db, $"{x}B", $"{x}H");
        for (var i = 0; i < 50; i++)
        {
            await BinarioDb.SembrarPerfilAsync(db, $"{x}E{i:00}", $"{x}B");
            (await motor.PlaceAsync($"{x}E{i:00}", $"{x}B")).IsSuccess.Should().BeTrue();
        }
        var t2 = await MideAsync("Colocar una raiz provisional con 50 en su equipo a mas de 8.000 niveles", async () =>
        {
            var r = await motor.PlaceAsync($"{x}B", $"{x}H");
            r.IsSuccess.Should().BeTrue(r.Error);
            r.Value!.Depth.Should().BeGreaterThan(8000);
        });

        // 3. Retirar ese subarbol (los hijos pasan a raices provisionales), con el arbol en exclusivo.
        var t3 = await MideAsync("Retirar un nodo con equipo a mas de 8.000 niveles", async () =>
        {
            await DualTeamGate.EnterTreeExclusiveAsync(db);
            (await DualTeamNodes.RemoveAsync(db, $"{x}B", DualTeamChildrenOnRemoval.Roots, DateTime.Now, "medicion")).Should().BeTrue();
        });

        await tx.RollbackAsync();
        new[] { t1, t2, t3 }.Should().OnlyContain(t => t < Objetivo);
    }

    [SqlServerFact]
    public async Task Lecturas_del_mayor_equipo()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadUncommitted);
        var (miembro, nodos) = await SqlServerDualSubtreeTests.MiembroConMayorEquipoAsync(db);
        var nodo = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == miembro);
        var servicio = new DualTeamService(db, CalificadoEnLosEquiposTests.Rangos(db), CalificadoEnLosEquiposTests.Calificacion(db));
        await servicio.GetMyTeamGridAsync(miembro, new GridDataRequest { PageSize = 20 });   // calienta

        var t1 = await MideAsync($"Mi equipo de {miembro} ({nodos} nodos), primera pagina", () =>
            servicio.GetMyTeamGridAsync(miembro, new GridDataRequest { PageSize = 20 }));
        var t2 = await MideAsync($"Mi equipo de {miembro}, pagina 1.000", () =>
            servicio.GetMyTeamGridAsync(miembro, new GridDataRequest { Page = 1000, PageSize = 20 }));
        // La parte binaria del progreso de rango (GetRankProgressHandler): una consulta, rango unido a las suscripciones activas.
        var t3 = await MideAsync($"Progreso de rango de {miembro}: suscripciones activas en su equipo binario", () =>
            (from s in db.MembershipSubscriptions.AsNoTracking()
             join d in DualSubtree.Under(db, nodo).AsNoTracking() on s.MemberId equals d.MemberId
             where s.SubscriptionStatus == MembershipStatus.Active
             select s.Id).CountAsync());

        await tx.RollbackAsync();
        new[] { t1, t2, t3 }.Should().OnlyContain(t => t < Objetivo);
    }

    /// <summary>
    /// Revision C1 e I1 sobre los datos reales: un nodo profundo con equipo se retira como lo hace el admin (su equipo
    /// queda flotando) y se vuelve a colocar en el mismo sitio. Las piernas de toda su linea (mas de 8.000 ascendientes)
    /// y las suyas vuelven exactamente a su valor, y su equipo vuelve bajo el por la clave. Todo en una transaccion que
    /// se deshace.
    /// </summary>
    [SqlServerFact]
    public async Task Retirar_y_recolocar_un_nodo_profundo_con_equipo_deja_las_piernas_igual()
    {
        await using var db = SqlServerReportes.Db();
        db.Database.SetCommandTimeout(600);   // deshacer reescribe rutas largas
        // Un nodo a mas de 4.000 niveles, con padre y con un equipo de 2 a 50 nodos.
        var x = await db.DualTeamTree.AsNoTracking()
            .Where(d => d.Depth > 4000 && d.ParentMemberId != null && d.LeftLegCount + d.RightLegCount >= 2 && d.LeftLegCount + d.RightLegCount <= 50)
            .OrderBy(d => d.Depth).ThenBy(d => d.Id).FirstAsync();
        var linea = await LineaAsync(db, x.MemberId);
        var antes = await db.DualTeamTree.AsNoTracking().Where(d => linea.Contains(d.MemberId))
            .ToDictionaryAsync(d => d.MemberId, d => (d.LeftLegPoints, d.RightLegPoints, d.LeftLegCount, d.RightLegCount));
        var equipo = await DualSubtree.Under(db, x).CountAsync();
        _salida.WriteLine($"Nodo {x.MemberId} a profundidad {x.Depth}, {equipo} en su equipo, {linea.Count - 1} ascendientes");

        await using var tx = await db.Database.BeginTransactionAsync();
        var t = await MideAsync("Retirar (admin) y recolocar en el mismo sitio un nodo profundo con equipo", async () =>
        {
            await DualTeamGate.EnterTreeExclusiveAsync(db);
            (await DualTeamNodes.RemoveAsync(db, x.MemberId, DualTeamChildrenOnRemoval.Floating, DateTime.Now, "medicion")).Should().BeTrue();
            var flotantes = await DualTeamNodes.FloatingChildrenAsync(db, x.MemberId);
            var padre = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == x.ParentMemberId);
            var nuevo = await DualTeamNodes.ChildAsync(db, padre, x.MemberId, x.Side, DateTime.Now, "medicion");
            db.DualTeamTree.Add(nuevo);
            await db.SaveChangesAsync();
            await LegPointMaintenance.ApplyIncrementalAsync(db, new[] { x.MemberId });
            await DualTeamNodes.ReattachFloatingChildrenAsync(db, x.MemberId, flotantes, DateTime.Now, "medicion");
        });

        var despues = await db.DualTeamTree.AsNoTracking().Where(d => linea.Contains(d.MemberId))
            .ToDictionaryAsync(d => d.MemberId, d => (d.LeftLegPoints, d.RightLegPoints, d.LeftLegCount, d.RightLegCount));
        var nodoX = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == x.MemberId);
        var equipoDespues = await DualSubtree.Under(db, nodoX).CountAsync();
        await tx.RollbackAsync();

        equipoDespues.Should().Be(equipo, "su equipo vuelve bajo el por la clave");
        despues.Should().BeEquivalentTo(antes, "la linea entera y el propio nodo quedan como estaban");
        t.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    private static async Task<List<string>> LineaAsync(Repository.Context.AppDbContext db, string memberId)
    {
        var linea = new List<string>();
        for (var actual = memberId; actual is not null;)
        {
            linea.Add(actual);
            var id = actual;
            actual = await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId == id).Select(d => d.ParentMemberId).FirstOrDefaultAsync();
        }
        return linea;
    }

    /// <summary>
    /// 10 colocaciones simultaneas en el mismo arbol a mas de 8.000 niveles (bajo 10 nodos distintos de la cadena mas
    /// honda, que comparten casi toda su linea): ninguna falla ni se agota, con p50 y p95. Se retiran al terminar.
    /// </summary>
    [SqlServerFact]
    public async Task Diez_colocaciones_simultaneas_a_gran_profundidad()
    {
        string x;
        List<string> patrocinadores;
        await using (var db = SqlServerReportes.Db())
        {
            x = BinarioDb.Prefijo("MD10");
            // Nodos a mas de 8.000 niveles con algun hueco libre (la cadena honda): cada colocacion va a su hueco, pero
            // todas suben piernas por casi la misma linea de 8.000 ascendientes, que es donde compiten.
            patrocinadores = await db.DualTeamTree.AsNoTracking()
                .Where(d => d.Depth > 8000 && db.DualTeamTree.Count(k => k.ParentMemberId == d.MemberId) < 2)
                .OrderByDescending(d => d.Depth).ThenBy(d => d.Id).Select(d => d.MemberId).Take(10).ToListAsync();
        }
        patrocinadores.Should().HaveCount(10);
        var tiempos = new System.Collections.Concurrent.ConcurrentBag<double>();
        try
        {
            var resultados = await Task.WhenAll(patrocinadores.Select((sp, i) => Task.Run(async () =>
            {
                await using var db = SqlServerReportes.Db();
                var c = Stopwatch.StartNew();
                var r = await new PlacementService(db, new Reloj()).PlaceAsync($"{x}{i:00}", sp);
                c.Stop();
                tiempos.Add(c.Elapsed.TotalMilliseconds);
                return r;
            })));

            var orden = tiempos.OrderBy(t => t).ToList();
            double P(double q) => orden[(int)Math.Ceiling(q * orden.Count) - 1];
            _salida.WriteLine($"10 colocaciones simultaneas a mas de 8.000 niveles: p50 {P(0.5):0} ms, p95 {P(0.95):0} ms, max {orden[^1]:0} ms");
            resultados.Should().OnlyContain(r => r.IsSuccess, string.Join("; ", resultados.Where(r => !r.IsSuccess).Select(r => r.Error)));
            resultados.Should().OnlyContain(r => r.Value!.Depth > 8000);
        }
        finally
        {
            // Se retiran (restando sus piernas) para dejar la copia como estaba.
            await using var db = SqlServerReportes.Db();
            await using var tx = await db.Database.BeginTransactionAsync();
            await DualTeamGate.EnterTreeExclusiveAsync(db);
            foreach (var id in await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(x)).Select(d => d.MemberId).ToListAsync())
                await DualTeamNodes.RemoveAsync(db, id, DualTeamChildrenOnRemoval.Reject, DateTime.Now, "medicion");
            await tx.CommitAsync();
            await BinarioDb.LimpiarAsync(x);
        }
    }
}
