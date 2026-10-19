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
}
