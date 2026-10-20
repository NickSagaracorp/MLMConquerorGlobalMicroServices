using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Binario;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan Q: mediciones sobre la base local (8.223 niveles) de lo que cambia el plan: la cola de piernas y su vaciado,
/// y (Tarea 20) el alta acotada. Solo con Medicion.runsettings.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
[Trait(SqlServerMedicionPlanMTests.Categoria, SqlServerMedicionPlanMTests.Medicion)]
public class SqlServerMedicionPlanQTests
{
    private readonly ITestOutputHelper _salida;

    public SqlServerMedicionPlanQTests(ITestOutputHelper salida) => _salida = salida;

    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>
    /// Revision del bloque 2 (I-3): vaciar <paramref name="n"/> deltas con arranques distintos en la linea honda (profundidad
    /// minima <paramref name="profMin"/>), dentro de una transaccion que se deshace. Antes de la union de caminos, 200 a mas
    /// de 8.000 niveles tardaban unos 40 s. Objetivo: 1.000 en la misma pierna, menos de 30 s.
    /// </summary>
    [SqlServerTheory]
    [InlineData(200, 8000)]
    [InlineData(1000, 7000)]
    public async Task Vaciar_deltas_profundos_en_la_misma_pierna(int n, int profMin)
    {
        await using var db = SqlServerReportes.Db();
        await LegPointMaintenance.DrainAsync(db);
        db.Database.SetCommandTimeout(900);
        await using var tx = await db.Database.BeginTransactionAsync();
        var arranques = await db.DualTeamTree.AsNoTracking().Where(d => d.Depth >= profMin && d.IsDeleted == false)
            .OrderByDescending(d => d.Depth).ThenBy(d => d.Id).Select(d => d.MemberId).Take(n).ToListAsync();
        arranques.Should().HaveCount(n);
        var ahora = DateTime.Now;
        for (var i = 0; i < arranques.Count; i++)
            db.DualLegDeltas.Add(new Domain.Entities.Tree.DualLegDelta
            {
                StartMemberId = arranques[i], Side = i % 2 == 0 ? Domain.Enums.TreeSide.Left : Domain.Enums.TreeSide.Right,
                Points = 1, NodeCount = 1, CreationDate = ahora, CreatedBy = "plan-q-medicion"
            });
        await db.SaveChangesAsync();

        var reloj = Stopwatch.StartNew();
        var aplicados = await LegPointMaintenance.DrainAsync(db);
        reloj.Stop();
        _salida.WriteLine($"Vaciar {n} deltas a {profMin}+ niveles: {reloj.ElapsedMilliseconds} ms ({aplicados} aplicados)");
        await tx.RollbackAsync();

        aplicados.Should().BeGreaterThanOrEqualTo(n);
        if (n >= 1000) reloj.ElapsedMilliseconds.Should().BeLessThan(30_000);
    }

    [SqlServerFact]
    public async Task Vaciar_en_bucle_durante_diez_colocaciones_a_gran_profundidad()
    {
        var x = BinarioDb.Prefijo("MDQ");
        List<string> patrocinadores;
        string cima;
        int antes;
        await using (var db = SqlServerReportes.Db())
        {
            await LegPointMaintenance.DrainAsync(db);
            patrocinadores = await db.DualTeamTree.AsNoTracking()
                .Where(d => d.Depth > 8000 && db.DualTeamTree.Count(k => k.ParentMemberId == d.MemberId) < 2)
                .OrderByDescending(d => d.Depth).ThenBy(d => d.Id).Select(d => d.MemberId).Take(10).ToListAsync();
            patrocinadores.Should().HaveCount(10);
            // La raiz de cada patrocinador (profundidad 0 de su clave): las 10 colocaciones suben por ella.
            var raices = new HashSet<string>();
            foreach (var sp in patrocinadores)
            {
                var clave = (await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == sp)).BinaryPath!;
                var claveRaiz = DualBinaryPath.Ancestor(clave, 0);
                raices.Add(await db.DualTeamTree.AsNoTracking().Where(d => d.BinaryPath == claveRaiz).Select(d => d.MemberId).SingleAsync());
            }
            raices.Should().ContainSingle("la cadena honda cuelga de una sola raiz");
            cima = raices.Single();
            var r0 = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == cima);
            antes = r0.LeftLegCount + r0.RightLegCount;
        }

        var tiempos = new ConcurrentBag<double>();
        var fallosDelVaciado = new ConcurrentBag<Exception>();
        using var parar = new CancellationTokenSource();
        try
        {
            var vaciador = Task.Run(async () =>
            {
                while (!parar.IsCancellationRequested)
                {
                    try
                    {
                        await using var db = SqlServerReportes.Db();
                        await LegPointMaintenance.DrainAsync(db);
                    }
                    catch (Exception ex) { fallosDelVaciado.Add(ex); }
                    await Task.Delay(50);
                }
            });

            var resultados = await Task.WhenAll(patrocinadores.Select((sp, i) => Task.Run(async () =>
            {
                await using var db = SqlServerReportes.Db();
                var c = Stopwatch.StartNew();
                var r = await new PlacementService(db, new Reloj()).PlaceAsync($"{x}{i:00}", sp);
                c.Stop();
                tiempos.Add(c.Elapsed.TotalMilliseconds);
                return r;
            })));
            parar.Cancel();
            await vaciador;

            var orden = tiempos.OrderBy(t => t).ToList();
            double P(double q) => orden[(int)Math.Ceiling(q * orden.Count) - 1];
            _salida.WriteLine($"10 colocaciones con el vaciado en bucle: p50 {P(0.5):0} ms, p95 {P(0.95):0} ms, max {orden[^1]:0} ms");
            resultados.Should().OnlyContain(r => r.IsSuccess, string.Join("; ", resultados.Where(r => !r.IsSuccess).Select(r => r.Error)));
            fallosDelVaciado.Should().BeEmpty("el vaciado no puede ser victima de interbloqueo ni agotar su tiempo");
            P(0.95).Should().BeLessThan(2000);

            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db);
                var r1 = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == cima);
                (r1.LeftLegCount + r1.RightLegCount).Should().Be(antes + 10, "cada colocacion sube una vez por la linea comun");
            }
        }
        finally
        {
            parar.Cancel();
            await using (var db = SqlServerReportes.Db())
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                await DualTeamGate.EnterTreeExclusiveAsync(db);
                foreach (var id in await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(x)).Select(d => d.MemberId).ToListAsync())
                    await DualTeamNodes.RemoveAsync(db, id, DualTeamChildrenOnRemoval.Reject, DateTime.Now, "medicion");
                await tx.CommitAsync();
            }
            await using (var db = SqlServerReportes.Db()) await LegPointMaintenance.DrainAsync(db);
            await BinarioDb.LimpiarAsync(x);
        }
    }

    /// <summary>
    /// Tarea 20: 1.000 altas bajo el miembro mas profundo de la inscripcion local, una fila SponsorLine por alta (como la
    /// activacion, Tarea 18). Quedan 1.000 filas (antes, 1.000 por la profundidad), el trabajo las aplica en menos de 10 s y
    /// la raiz suma 1.000 a su equipo. En una transaccion que se deshace.
    /// </summary>
    [SqlServerFact]
    public async Task Mil_altas_bajo_el_miembro_mas_profundo_se_aplican_en_menos_de_10_s()
    {
        await using var db = SqlServerReportes.Db();
        db.Database.SetCommandTimeout(300);
        var hondo = await db.GenealogyTree.AsNoTracking().Where(g => g.Node != null)
            .OrderByDescending(g => g.Level).ThenBy(g => g.Id).FirstAsync();
        var nodoRaiz = hondo.Node!.GetAncestor(hondo.Node.GetLevel() - 1);
        var raiz = await db.GenealogyTree.AsNoTracking().Where(g => g.Node == nodoRaiz).Select(g => g.MemberId).SingleAsync();
        int Equipo(List<Domain.Entities.Member.MemberStatisticEntity> s) => s.OrderBy(x => x.Id).First().EnrollmentTeamSize;
        _salida.WriteLine($"Miembro mas profundo de inscripcion: {hondo.MemberId}, nivel {hondo.Level}; raiz {raiz}");

        await using var tx = await db.Database.BeginTransactionAsync();
        var antes = Equipo(await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == raiz).ToListAsync());
        // Los pendientes de la base local no son de esta medicion: se dan por aplicados dentro de la transaccion.
        await db.Database.ExecuteSqlRawAsync("UPDATE MemberStatisticDeltas SET IsApplied = 1 WHERE IsApplied = 0");
        for (var i = 0; i < 1000; i++)
            db.MemberStatisticDeltas.Add(new Domain.Entities.Member.MemberStatisticDelta
            {
                MemberId = hondo.MemberId, Scope = Domain.Entities.Member.MemberStatisticDeltaScope.SponsorLine,
                EnrollmentPointsDelta = 6, EnrollmentTeamSizeDelta = 1, QualifiedSponsoredMembersDelta = 1,
                SourceMemberId = $"MEDQ{i:0000}", CreatedBy = "medicion", CreationDate = DateTime.Now
            });
        await db.SaveChangesAsync();
        (await db.MemberStatisticDeltas.CountAsync(d => !d.IsApplied)).Should().Be(1000, "una fila por alta, no una por ascendiente");

        var t = Stopwatch.StartNew();
        await new Repository.Jobs.ApplyMemberStatisticDeltasJob(db, new Reloj(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Repository.Jobs.ApplyMemberStatisticDeltasJob>.Instance).ExecuteAsync();
        t.Stop();
        var despues = Equipo(await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == raiz).ToListAsync());
        await tx.RollbackAsync();

        _salida.WriteLine($"Aplicar 1.000 altas a {hondo.Level} niveles: {t.ElapsedMilliseconds} ms");
        (despues - antes).Should().Be(1000);
        t.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }
}
