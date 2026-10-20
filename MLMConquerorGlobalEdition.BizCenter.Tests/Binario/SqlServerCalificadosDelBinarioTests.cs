using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>Plan Q (DQ13): los calificados del binario se mantienen por la cola, con activaciones, colocaciones, movimientos y retiradas.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerCalificadosDelBinarioTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private static async Task<string> SuscripcionAsync(string memberId, MembershipStatus estado)
    {
        await using var db = SqlServerReportes.Db();
        var s = ReportesDb.Suscripcion(db, memberId, 2, estado, DateTime.Now);
        await db.SaveChangesAsync();
        return s.Id;
    }

    private static async Task EstadoAsync(string id, MembershipStatus estado)
    {
        await using var db = SqlServerReportes.Db();
        (await db.MembershipSubscriptions.SingleAsync(s => s.Id == id)).SubscriptionStatus = estado;
        await db.SaveChangesAsync();
    }

    private static async Task<DualTeamEntity> NodoAsync(AppDbContext db, string id) =>
        await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == id);

    [SqlServerFact]
    public async Task Activar_y_cancelar_suben_y_bajan_por_la_linea()
    {
        var x = BinarioDb.Prefijo("PQK");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarPerfilesAsync(db, $"{x}P", $"{x}L", $"{x}N");
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left),
                    ($"{x}N", $"{x}L", TreeSide.Right));
            }
            var sus = await SuscripcionAsync($"{x}N", MembershipStatus.Active);
            await using (var db = SqlServerReportes.Db())
            {
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == $"{x}N" && d.OwnChange && d.QualifiedCount == 1))
                    .Should().Be(1, "la activacion solo encola");
                (await NodoAsync(db, $"{x}P")).LeftLegQualified.Should().Be(0);
                await LegPointMaintenance.DrainAsync(db);
                var (p, l, n) = (await NodoAsync(db, $"{x}P"), await NodoAsync(db, $"{x}L"), await NodoAsync(db, $"{x}N"));
                (p.LeftLegQualified, p.RightLegQualified, l.RightLegQualified, l.LeftLegQualified, n.SelfQualified)
                    .Should().Be((1, 0, 1, 0, 1));
                (n.LeftLegQualified + n.RightLegQualified).Should().Be(0, "lo propio no va a las piernas propias");
            }
            await EstadoAsync(sus, MembershipStatus.Cancelled);
            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db);
                await CalificadosCoherentesAsync(db, x);
                (await NodoAsync(db, $"{x}P")).LeftLegQualified.Should().Be(0);
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }

    [SqlServerFact]
    public async Task Colocar_con_la_suscripcion_activa_o_sin_ella()
    {
        var x = BinarioDb.Prefijo("PQM");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left));
                await BinarioDb.SembrarPerfilAsync(db, $"{x}A", $"{x}L");   // activa antes de tener nodo
                await BinarioDb.SembrarPerfilAsync(db, $"{x}K", $"{x}L");   // pendiente
            }
            await SuscripcionAsync($"{x}A", MembershipStatus.Active);
            await SuscripcionAsync($"{x}K", MembershipStatus.Pending);
            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db);
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == $"{x}A" && d.OwnChange && d.CreatedBy == QualifiedTeamDeltas.Actor))
                    .Should().Be(1, "sin nodo, el propio espera en la cola");
                var motor = new PlacementService(db, new Reloj());
                (await motor.PlaceAsync($"{x}A", $"{x}L")).IsSuccess.Should().BeTrue();
                (await motor.PlaceAsync($"{x}K", $"{x}L")).IsSuccess.Should().BeTrue();
            }
            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db);
                await CalificadosCoherentesAsync(db, x);
                var p = await NodoAsync(db, $"{x}P");
                (p.LeftLegQualified, p.LeftLegCount).Should().Be((1, 3), "A cuenta una vez y K no cuenta");
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId.StartsWith(x))).Should().Be(0);
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }

    /// <summary>
    /// DQ2 con propios: C se activa dentro de la raiz provisional B (pendiente sin aplicar) y B se coloca; E, activa y con
    /// el propio aplicado, se retira con su hijo F flotando y F se activa mientras flota; E vuelve a colocarse (F con el).
    /// Tras vaciar, cada nodo es igual al recuento de su rango.
    /// </summary>
    [SqlServerFact]
    public async Task Con_movimientos_y_retiradas_por_medio_los_calificados_quedan_exactos()
    {
        var x = BinarioDb.Prefijo("PQN");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left));
                foreach (var (id, pat) in new[] { ("B", "A"), ("C", "B"), ("E", "A"), ("F", "E") })
                    await BinarioDb.SembrarPerfilAsync(db, $"{x}{id}", $"{x}{pat}");
                var motor = new PlacementService(db, new Reloj());
                (await motor.PlaceAsync($"{x}C", $"{x}B")).IsSuccess.Should().BeTrue();   // B sigue sin colocar: raiz provisional
                (await motor.PlaceAsync($"{x}E", $"{x}A")).IsSuccess.Should().BeTrue();
                (await motor.PlaceAsync($"{x}F", $"{x}E")).IsSuccess.Should().BeTrue();
            }
            await SuscripcionAsync($"{x}E", MembershipStatus.Active);
            await using (var db = SqlServerReportes.Db()) await LegPointMaintenance.DrainAsync(db);   // E aplicado
            await SuscripcionAsync($"{x}C", MembershipStatus.Active);                                // pendiente
            await using (var db = SqlServerReportes.Db())
                (await new PlacementService(db, new Reloj()).PlaceAsync($"{x}B", $"{x}A")).IsSuccess.Should().BeTrue();
            await using (var db = SqlServerReportes.Db())
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                await DualTeamGate.EnterTreeExclusiveAsync(db);
                (await DualTeamNodes.RemoveAsync(db, $"{x}E", DualTeamChildrenOnRemoval.Floating, DateTime.Now, "plan-q-prueba")).Should().BeTrue();
                await tx.CommitAsync();
                (await db.DualLegDeltas.Where(d => d.StartMemberId == $"{x}E" && d.OwnChange).SumAsync(d => d.QualifiedCount))
                    .Should().Be(1, "la retirada devuelve a la cola el propio ya aplicado de E");
            }
            await SuscripcionAsync($"{x}F", MembershipStatus.Active);   // F se activa mientras flota
            await using (var db = SqlServerReportes.Db())
                (await new PlacementService(db, new Reloj()).PlaceAsync($"{x}E", $"{x}A")).IsSuccess.Should().BeTrue();
            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db);
                await CalificadosCoherentesAsync(db, x);
                var p = await NodoAsync(db, $"{x}P");
                p.LeftLegQualified.Should().Be(3, "C, E y F, cada uno una vez");
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }

    /// <summary>
    /// Cada nodo vivo del prefijo: SelfQualified = sus suscripciones activas y cada pierna = las activas de su rango (el
    /// recuento al vuelo al que sustituyen, por DualSubtree.Leg). DualTeamSize = LeftLegCount + RightLegCount (DQ15).
    /// </summary>
    public static async Task CalificadosCoherentesAsync(AppDbContext db, string prefijo)
    {
        var activas = db.MembershipSubscriptions.AsNoTracking().Where(s => s.SubscriptionStatus == MembershipStatus.Active);
        foreach (var n in await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(prefijo)).ToListAsync())
        {
            n.SelfQualified.Should().Be(await activas.CountAsync(s => s.MemberId == n.MemberId), $"{n.MemberId} propio");
            foreach (var lado in new[] { TreeSide.Left, TreeSide.Right })
            {
                var ids = DualSubtree.Leg(db, n, lado).Select(d => d.MemberId);
                var esperado = await activas.CountAsync(s => ids.Contains(s.MemberId));
                (lado == TreeSide.Left ? n.LeftLegQualified : n.RightLegQualified).Should().Be(esperado, $"{n.MemberId} pierna {lado}");
            }
            var tamano = await db.MemberStatistics.AsNoTracking().Where(s => s.MemberId == n.MemberId)
                .OrderBy(s => s.Id).Select(s => (int?)s.DualTeamSize).FirstOrDefaultAsync();
            if (tamano is not null) tamano.Should().Be(n.LeftLegCount + n.RightLegCount, $"{n.MemberId} DualTeamSize");
        }
    }
}
