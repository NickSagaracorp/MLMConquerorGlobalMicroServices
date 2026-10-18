using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Features.Placement.PlaceMember;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Revision I1 y C1 del Plan P: un miembro X retirado por el admin deja a su equipo esperando (hijos flotantes: cuelgan
/// de X por ParentMemberId y, por la clave, de una raiz virtual). Cuando X vuelve a colocarse por CUALQUIER camino, su
/// equipo vuelve con el: Under(X) lo ve, los contadores y puntos de X y de su linea nueva cuadran, ninguna pierna queda
/// negativa y X puede colocar a otro debajo. Aqui, el motor (y por tanto AutoPlacementJob) y el BizCenter.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerFlotantesTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>P (raiz) → X → C → D, con puntos; despues el admin retira a X y C y D quedan flotando.</summary>
    internal static async Task PrepararAsync(string x)
    {
        await BinarioDb.LimpiarAsync(x);
        await using var db = SqlServerReportes.Db();
        await BinarioDb.SembrarPerfilAsync(db, $"{x}P", null);
        await BinarioDb.SembrarPerfilAsync(db, $"{x}X", $"{x}P");
        await BinarioDb.SembrarPerfilAsync(db, $"{x}C", $"{x}X");
        await BinarioDb.SembrarPerfilAsync(db, $"{x}D", $"{x}C");
        await BinarioDb.SembrarPerfilAsync(db, $"{x}Y", $"{x}X");
        await BinarioDb.PuntosAsync(db, $"{x}X", 1);
        await BinarioDb.PuntosAsync(db, $"{x}C", 10);
        await BinarioDb.PuntosAsync(db, $"{x}D", 5);
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}X", $"{x}P", TreeSide.Left),
            ($"{x}C", $"{x}X", TreeSide.Left), ($"{x}D", $"{x}C", TreeSide.Left));

        await using var tx = await db.Database.BeginTransactionAsync();
        await DualTeamGate.EnterTreeExclusiveAsync(db);
        (await DualTeamNodes.RemoveAsync(db, $"{x}X", DualTeamChildrenOnRemoval.Floating, DateTime.Now, "plan-p-prueba")).Should().BeTrue();
        await tx.CommitAsync();
    }

    /// <summary>X ya colocado de nuevo bajo P: su equipo vuelve con el y todo cuadra.</summary>
    internal static async Task ComprobarAsync(string x)
    {
        await using var db = SqlServerReportes.Db();
        var nodos = await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(x)).ToDictionaryAsync(d => d.MemberId[x.Length..]);
        nodos["X"].ParentMemberId.Should().Be($"{x}P");
        (await DualSubtree.Under(db, nodos["X"]).Select(d => d.MemberId).ToListAsync())
            .Should().BeEquivalentTo(new[] { $"{x}C", $"{x}D" }, "el equipo de X esta bajo X por la clave");
        (nodos["X"].LeftLegPoints, nodos["X"].LeftLegCount).Should().Be((15m, 2));
        (nodos["P"].LeftLegPoints, nodos["P"].LeftLegCount).Should().Be((16m, 3));
        nodos.Values.Should().OnlyContain(n => n.LeftLegPoints >= 0 && n.RightLegPoints >= 0 && n.LeftLegCount >= 0 && n.RightLegCount >= 0);

        // X puede colocar a otro debajo: tiene el izquierdo ocupado por C, asi que Y va a su derecha.
        var y = await new PlacementService(db, new Reloj()).PlaceAsync($"{x}Y", $"{x}X");
        y.IsSuccess.Should().BeTrue(y.Error);
        (y.Value!.ParentMemberId, y.Value.Side).Should().Be(($"{x}X", TreeSide.Right));
    }

    [SqlServerFact]
    public async Task El_motor_y_el_job_recolocan_al_retirado_con_su_equipo()
    {
        var x = BinarioDb.Prefijo("PFM");
        try
        {
            await PrepararAsync(x);
            // Es lo que hace AutoPlacementJob: X no tiene nodo, asi que esta pendiente (PendingPlacements).
            await using (var db = SqlServerReportes.Db())
            {
                (await PendingPlacements.Query(db).AnyAsync(p => p.MemberId == $"{x}X")).Should().BeTrue();
                var r = await new PlacementService(db, new Reloj()).PlaceBulkAsync(new[] { ($"{x}X", $"{x}P") });
                r.Value!.Placed.Should().Be(1);
            }
            await ComprobarAsync(x);
        }
        finally
        {
            await BinarioDb.LimpiarAsync(x);
        }
    }

    [SqlServerFact]
    public async Task El_BizCenter_recoloca_al_retirado_con_su_equipo()
    {
        var x = BinarioDb.Prefijo("PFB");
        try
        {
            await PrepararAsync(x);
            await using (var db = SqlServerReportes.Db())
            {
                var usuario = new Mock<BizCenter.Services.ICurrentUserService>();
                usuario.Setup(u => u.IsAdmin).Returns(true);
                usuario.Setup(u => u.UserId).Returns("plan-p-prueba");
                var reloj = new Mock<BizCenter.Services.IDateTimeProvider>();
                reloj.Setup(r => r.Now).Returns(DateTime.Now);
                var r = await new PlaceMemberHandler(db, usuario.Object, reloj.Object)
                    .Handle(new PlaceMemberCommand($"{x}X", $"{x}P", "Left"), CancellationToken.None);
                r.IsSuccess.Should().BeTrue(r.Error);
            }
            await ComprobarAsync(x);
        }
        finally
        {
            await BinarioDb.LimpiarAsync(x);
        }
    }
}
