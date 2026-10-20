using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Revision del bloque 2 (M-3): un arranque cuyos deltas hacen fallar la tanda siempre (aqui, unos puntos que desbordan
/// decimal(18,4) en su pierna) no bloquea la cola para siempre: la ventana se parte hasta aislarlo, se le anotan los
/// intentos y, al tercero, sus deltas se apartan con el error. Los demas se aplican.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerVenenoEnLaColaTests
{
    [SqlServerFact]
    public async Task Un_arranque_que_siempre_falla_se_aparta_al_tercer_intento_y_no_bloquea_a_los_demas()
    {
        var x = BinarioDb.Prefijo("PQV");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left),
                    ($"{x}B", $"{x}P", TreeSide.Right));
                db.DualLegDeltas.AddRange(
                    new DualLegDelta { StartMemberId = $"{x}A", Side = TreeSide.Left, Points = 90_000_000_000_000m, CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba" },
                    new DualLegDelta { StartMemberId = $"{x}A", Side = TreeSide.Left, Points = 90_000_000_000_000m, CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba" },
                    new DualLegDelta { StartMemberId = $"{x}B", Side = TreeSide.Left, Points = 7, NodeCount = 1, CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba" });
                await db.SaveChangesAsync();
            }

            // Tres pasadas: cada una aisla a A, le anota un intento y termina con el error.
            for (var i = 1; i <= LegPointMaintenance.MaxAttempts; i++)
            {
                await using var db = SqlServerReportes.Db();
                var fallo = async () => await LegPointMaintenance.DrainAsync(db, isolateFailures: true);
                (await fallo.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(8115);
            }

            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db, isolateFailures: true);
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId.StartsWith(x))).Should().Be(0);
                var apartados = await db.DualLegDeltaDeadLetters.Where(d => d.StartMemberId == $"{x}A").ToListAsync();
                apartados.Should().HaveCount(2).And.OnlyContain(d => d.Reason.StartsWith("fallo: 8115") && d.Attempts == LegPointMaintenance.MaxAttempts);
                var b = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}B");
                (b.LeftLegPoints, b.LeftLegCount).Should().Be((7m, 1), "el delta de B se aplica");
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }

    /// <summary>
    /// Revision 2 del bloque 2 (I1): fuera del trabajo de cada minuto (comisiones, instantanea, rango) un fallo de datos se
    /// propaga sin partir la ventana ni anotar intentos: esos lectores no pueden quedarse decenas de minutos aislando un
    /// arranque, y apartar es cosa del trabajo.
    /// </summary>
    [SqlServerFact]
    public async Task Fuera_del_trabajo_un_fallo_de_datos_no_parte_la_ventana_ni_anota_intentos()
    {
        var x = BinarioDb.Prefijo("PQW");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left));
                db.DualLegDeltas.AddRange(
                    new DualLegDelta { StartMemberId = $"{x}A", Side = TreeSide.Left, Points = 90_000_000_000_000m, CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba" },
                    new DualLegDelta { StartMemberId = $"{x}A", Side = TreeSide.Left, Points = 90_000_000_000_000m, CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba" });
                await db.SaveChangesAsync();
            }

            for (var i = 1; i <= LegPointMaintenance.MaxAttempts + 1; i++)
            {
                await using var db = SqlServerReportes.Db();
                var fallo = async () => await LegPointMaintenance.DrainAsync(db);
                (await fallo.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(8115);
            }

            await using (var db = SqlServerReportes.Db())
            {
                (await db.DualLegDeltas.Where(d => d.StartMemberId == $"{x}A").Select(d => d.Attempts).ToListAsync())
                    .Should().HaveCount(2).And.OnlyContain(a => a == 0);
                (await db.DualLegDeltaDeadLetters.CountAsync(d => d.StartMemberId.StartsWith(x))).Should().Be(0);
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }

    /// <summary>
    /// Revision 2 del bloque 2 (I1): un tiempo agotado (-2) no es culpa de ningun arranque: ni en el trabajo de cada minuto
    /// parte la ventana ni anota intentos, asi que unos deltas sanos nunca se apartan por un bloqueo o una caida.
    /// </summary>
    [SqlServerFact]
    public async Task Un_tiempo_agotado_no_cuenta_como_intento_ni_aparta_deltas()
    {
        var x = BinarioDb.Prefijo("PQT");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left));
                db.DualLegDeltas.Add(new DualLegDelta { StartMemberId = $"{x}A", Side = TreeSide.Left, Points = 5, NodeCount = 0, CreationDate = DateTime.Now, CreatedBy = "plan-q-prueba" });
                await db.SaveChangesAsync();
            }

            // Otra conexion bloquea la fila del ascendiente que la tanda tiene que actualizar.
            await using (var bloqueo = new SqlConnection(SqlServerReportes.ConnectionString))
            {
                await bloqueo.OpenAsync();
                await using var tx = (SqlTransaction)await bloqueo.BeginTransactionAsync();
                await using (var cmd = new SqlCommand("UPDATE DualTeamTree SET LastUpdateBy = N'plan-q-bloqueo' WHERE MemberId = @p", bloqueo, tx))
                {
                    cmd.Parameters.AddWithValue("@p", $"{x}P");
                    await cmd.ExecuteNonQueryAsync();
                }

                for (var i = 1; i <= LegPointMaintenance.MaxAttempts + 1; i++)
                {
                    // Con los servicios -run vaciando cada minuto, el candado de la cola puede estar ocupado un momento
                    // (DualTeamBusyException, espera de 1 s): esa vuelta no cuenta y se repite.
                    for (var vuelta = 1; ; vuelta++)
                    {
                        await using var db = SqlServerReportes.Db();
                        try
                        {
                            await LegPointMaintenance.DrainAsync(db, lockTimeoutMs: 1_000, isolateFailures: true,
                                options: new LegDrainOptions { BatchCommandSeconds = 2 });
                            throw new InvalidOperationException("El vaciado tenia que agotar su tiempo.");
                        }
                        catch (DualTeamBusyException) when (vuelta < 30) { await Task.Delay(1_000); }
                        catch (SqlException ex) { ex.Number.Should().Be(-2); break; }
                    }
                }
                await tx.RollbackAsync();
            }

            await using (var db = SqlServerReportes.Db())
            {
                (await db.DualLegDeltas.Where(d => d.StartMemberId == $"{x}A").Select(d => d.Attempts).SingleAsync()).Should().Be(0);
                (await db.DualLegDeltaDeadLetters.CountAsync(d => d.StartMemberId.StartsWith(x))).Should().Be(0);
                await LegPointMaintenance.DrainAsync(db, isolateFailures: true);
                var p = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}P");
                (p.LeftLegPoints, p.LeftLegCount).Should().Be((5m, 1), "sin el bloqueo se aplica (el nodo de A ya contaba)");
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }
}
