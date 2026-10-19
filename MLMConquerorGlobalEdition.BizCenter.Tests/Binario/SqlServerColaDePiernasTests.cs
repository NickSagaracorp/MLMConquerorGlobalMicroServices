using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>Plan Q (P-R9): colocar encola; el vaciado deja las piernas exactas, con movimientos y retiradas por medio.</summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerColaDePiernasTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    [SqlServerFact]
    public async Task Colocar_no_toca_la_linea_y_el_vaciado_la_sube()
    {
        var x = BinarioDb.Prefijo("PQC");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left));   // ya vacia (Paso 5)
                await BinarioDb.SembrarPerfilAsync(db, $"{x}N", $"{x}L");
                await BinarioDb.PuntosAsync(db, $"{x}N", 25);
                (await new PlacementService(db, new Reloj()).PlaceAsync($"{x}N", $"{x}L")).IsSuccess.Should().BeTrue();
            }
            await using (var db = SqlServerReportes.Db())
            {
                var p = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}P");
                p.LeftLegCount.Should().Be(1, "la colocacion solo encola");
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == $"{x}L")).Should().Be(1);

                await LegPointMaintenance.DrainAsync(db);

                p = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}P");
                (p.LeftLegCount, p.LeftLegPoints).Should().Be((2, 25m));
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == $"{x}L")).Should().Be(0);
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }

    /// <summary>
    /// DQ2: colocaciones con deltas pendientes DENTRO de una raiz provisional que despues se coloca, y una retirada con
    /// hijos flotantes con pendientes. Tras vaciar, cada pierna es igual al recuento de su rango.
    /// </summary>
    [SqlServerFact]
    public async Task Con_movimientos_y_retiradas_por_medio_el_vaciado_deja_las_piernas_exactas()
    {
        var x = BinarioDb.Prefijo("PQD");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left));   // ya vacia (Paso 5)
                foreach (var (id, pat, pts) in new[] { ("A", "P", 0), ("B", "A", 10), ("C", "B", 20), ("D", "C", 40), ("E", "B", 5), ("F", "E", 7) })
                {
                    await BinarioDb.SembrarPerfilAsync(db, $"{x}{id}", $"{x}{pat}");
                    if (pts > 0) await BinarioDb.PuntosAsync(db, $"{x}{id}", pts);
                }
            }
            await using (var db = SqlServerReportes.Db())
            {
                var motor = new PlacementService(db, new Reloj());
                // B sin colocar: C, D, E y F cuelgan de la raiz provisional B, con sus deltas SIN aplicar.
                foreach (var (id, pat) in new[] { ("C", "B"), ("D", "C"), ("E", "B"), ("F", "E") })
                    (await motor.PlaceAsync($"{x}{id}", $"{x}{pat}")).IsSuccess.Should().BeTrue();
                // Se coloca B bajo A con pendientes dentro (DQ3: un delta con lo aplicado de B).
                (await motor.PlaceAsync($"{x}B", $"{x}A")).IsSuccess.Should().BeTrue();
            }
            await using (var db = SqlServerReportes.Db())
            {
                // Retirada de E con su hijo F flotando, sin haber vaciado.
                await using var tx = await db.Database.BeginTransactionAsync();
                await DualTeamGate.EnterTreeExclusiveAsync(db);
                (await DualTeamNodes.RemoveAsync(db, $"{x}E", DualTeamChildrenOnRemoval.Floating, DateTime.Now, "plan-q-prueba")).Should().BeTrue();
                await tx.CommitAsync();
            }
            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db);
                await PiernasCoherentesAsync(db, x);
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }

    /// <summary>
    /// Revision C1: F se coloca bajo E (pendiente con arranque en E), el admin retira a E con F flotando y E se vuelve a
    /// colocar (F vuelve con el) ANTES de vaciar. El pendiente de F no puede subir por el nodo nuevo de E: F ya vuelve con
    /// el +T de su subarbol. Tras vaciar, F cuenta una sola vez.
    /// </summary>
    [SqlServerFact]
    public async Task Retirar_y_recolocar_antes_de_vaciar_no_cuenta_dos_veces()
    {
        var x = BinarioDb.Prefijo("PQE");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}B", $"{x}P", TreeSide.Left));   // ya vacia
                foreach (var (id, pat, pts) in new[] { ("E", "B", 5), ("F", "E", 7) })
                {
                    await BinarioDb.SembrarPerfilAsync(db, $"{x}{id}", $"{x}{pat}");
                    await BinarioDb.PuntosAsync(db, $"{x}{id}", pts);
                }
            }
            await using (var db = SqlServerReportes.Db())
            {
                var motor = new PlacementService(db, new Reloj());
                (await motor.PlaceAsync($"{x}E", $"{x}B")).IsSuccess.Should().BeTrue();
                (await motor.PlaceAsync($"{x}F", $"{x}E")).IsSuccess.Should().BeTrue();   // pendiente con arranque en E
            }
            await using (var db = SqlServerReportes.Db())
            {
                await using var tx = await db.Database.BeginTransactionAsync();
                await DualTeamGate.EnterTreeExclusiveAsync(db);
                (await DualTeamNodes.RemoveAsync(db, $"{x}E", DualTeamChildrenOnRemoval.Floating, DateTime.Now, "plan-q-prueba")).Should().BeTrue();
                await tx.CommitAsync();
                (await db.DualLegDeltas.CountAsync(d => d.StartMemberId == $"{x}E"))
                    .Should().Be(0, "la retirada descarta los pendientes con arranque en el nodo retirado");
            }
            await using (var db = SqlServerReportes.Db())
            {
                var r = await new PlacementService(db, new Reloj()).PlaceAsync($"{x}E", $"{x}B");   // vuelve con F
                r.IsSuccess.Should().BeTrue(r.Error);
            }
            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db);
                await PiernasCoherentesAsync(db, x);
                var p = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}P");
                (p.LeftLegCount, p.LeftLegPoints).Should().Be((3, 12m), "B (0), E (5) y F (7), cada uno una vez");
            }
        }
        finally { await BinarioDb.LimpiarAsync(x); }
    }

    /// <summary>Cada nodo vivo del prefijo: sus contadores y puntos de pierna = el recuento de su rango por la clave.</summary>
    public static async Task PiernasCoherentesAsync(AppDbContext db, string prefijo)
    {
        var nodos = await db.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(prefijo)).ToListAsync();
        foreach (var n in nodos)
            foreach (var lado in new[] { TreeSide.Left, TreeSide.Right })
            {
                var ids = await DualSubtree.Leg(db, n, lado).AsNoTracking().Select(d => d.MemberId).ToListAsync();
                var pts = await db.MemberStatistics.AsNoTracking().Where(s => ids.Contains(s.MemberId))
                    .GroupBy(s => s.MemberId).Select(g => g.OrderBy(s => s.Id).First().PersonalPoints).SumAsync();
                var (c, p) = lado == TreeSide.Left ? (n.LeftLegCount, n.LeftLegPoints) : (n.RightLegCount, n.RightLegPoints);
                (c, p).Should().Be((ids.Count, (decimal)pts), $"{n.MemberId} pierna {lado}");
            }
    }
}
