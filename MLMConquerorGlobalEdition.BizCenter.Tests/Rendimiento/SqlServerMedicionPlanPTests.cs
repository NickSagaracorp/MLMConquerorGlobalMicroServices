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
}
