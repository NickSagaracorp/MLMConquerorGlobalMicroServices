using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Jobs;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

public class ApplyDualLegDeltasJobTests
{
    [Fact]
    public async Task En_InMemory_no_hay_cola_y_termina_sin_error()
    {
        await using var db = ReportesDb.Nueva();
        var job = new ApplyDualLegDeltasJob(db, NullLogger<ApplyDualLegDeltasJob>.Instance);
        (await job.ExecuteAsync()).Should().Be(0);
    }

    /// <summary>
    /// Revision 2 del bloque 2 (I1): DualLegDeltaDeadLetters debe estar vacia (Q-R30). Mientras tenga filas, cada pasada del
    /// trabajo lo registra como error con la cuenta, para que el aviso no dependa de mirar la tabla.
    /// </summary>
    [Fact]
    public async Task Con_deltas_apartados_cada_pasada_registra_un_error_con_la_cuenta()
    {
        await using var db = ReportesDb.Nueva();
        db.DualLegDeltaDeadLetters.AddRange(
            new DualLegDeltaDeadLetter { DeltaId = 1, StartMemberId = "A", Points = 5, Reason = "fallo: 8115", CreatedBy = "prueba" },
            new DualLegDeltaDeadLetter { DeltaId = 2, StartMemberId = "B", Points = 5, Reason = "sin-nodo", CreatedBy = "prueba" });
        await db.SaveChangesAsync();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<ApplyDualLegDeltasJob>>();

        await new ApplyDualLegDeltasJob(db, logger.Object).ExecuteAsync();

        logger.Verify(l => l.Log(
            Microsoft.Extensions.Logging.LogLevel.Error,
            It.IsAny<Microsoft.Extensions.Logging.EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("2") && v.ToString()!.Contains("DualLegDeltaDeadLetters")),
            It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }
}

/// <summary>
/// Revision del bloque 2 (I-1): una ejecucion del trabajo que encuentra otra en marcha se salta en el acto. Con
/// DisableConcurrentExecution(3600) esperaba hasta una hora ocupando un trabajador de la cola "bizcenter".
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerApplyDualLegDeltasJobTests
{
    [SqlServerFact]
    public async Task Con_otra_ejecucion_en_marcha_se_salta_sin_esperar()
    {
        var x = BinarioDb.Prefijo("PQJ");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left));
                db.DualLegDeltas.Add(new DualLegDelta
                {
                    StartMemberId = $"{x}L", Side = TreeSide.Left, Points = 3, NodeCount = 1,
                    CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba"
                });
                await db.SaveChangesAsync();
            }

            // Otra ejecucion "en marcha": su candado de sesion.
            await using (var otra = new SqlConnection(SqlServerReportes.ConnectionString))
            {
                await otra.OpenAsync();
                await using (var tomar = new SqlCommand(
                    "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @res, @LockMode = 'Exclusive', @LockOwner = 'Session', @LockTimeout = 0; SELECT @r;", otra))
                {
                    tomar.Parameters.AddWithValue("@res", ApplyDualLegDeltasJob.RunLockResource);
                    ((int)(await tomar.ExecuteScalarAsync())!).Should().BeGreaterThanOrEqualTo(0);
                }

                await using var db = SqlServerReportes.Db();
                var reloj = Stopwatch.StartNew();
                var n = await new ApplyDualLegDeltasJob(db, NullLogger<ApplyDualLegDeltasJob>.Instance).ExecuteAsync();
                reloj.Stop();

                n.Should().Be(0);
                reloj.ElapsedMilliseconds.Should().BeLessThan(2000, "se salta, no espera a la otra ejecucion");
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == $"{x}L")).Should().Be(1, "no vacia mientras la otra corre");

                // La conexion vuelve al pool sin reiniciarse: el candado de sesion se suelta a mano.
                await using var soltar = new SqlCommand("EXEC sp_releaseapplock @Resource = @res, @LockOwner = 'Session';", otra);
                soltar.Parameters.AddWithValue("@res", ApplyDualLegDeltasJob.RunLockResource);
                await soltar.ExecuteNonQueryAsync();
            }

            await using (var db = SqlServerReportes.Db())
            {
                (await new ApplyDualLegDeltasJob(db, NullLogger<ApplyDualLegDeltasJob>.Instance).ExecuteAsync())
                    .Should().BeGreaterThanOrEqualTo(1, "sin otra ejecucion, vacia");
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == $"{x}L")).Should().Be(0);
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }
}
