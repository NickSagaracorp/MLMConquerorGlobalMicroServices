using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
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
                var fallo = async () => await LegPointMaintenance.DrainAsync(db);
                (await fallo.Should().ThrowAsync<SqlException>()).Which.Number.Should().Be(8115);
            }

            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db);
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId.StartsWith(x))).Should().Be(0);
                var apartados = await db.DualLegDeltaDeadLetters.Where(d => d.StartMemberId == $"{x}A").ToListAsync();
                apartados.Should().HaveCount(2).And.OnlyContain(d => d.Reason.StartsWith("fallo: 8115") && d.Attempts == LegPointMaintenance.MaxAttempts);
                var b = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}B");
                (b.LeftLegPoints, b.LeftLegCount).Should().Be((7m, 1), "el delta de B se aplica");
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }
}
