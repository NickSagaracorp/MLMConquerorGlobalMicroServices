using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Domain.Services;
using MLMConquerorGlobalEdition.Repository.Queries;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Decision de Nick (2026-10-18), con su ejemplo: A patrocina a B y B patrocina a C. B aun no esta colocado,
/// pero ya puede colocar a C: B existe como RAIZ PROVISIONAL con su subarbol. Los puntos de C cuentan para B y
/// los de B todavia no suben a A. Cuando alguien coloca a B, su subarbol se mueve entero, sin cambiar de forma,
/// al destino de las reglas a/b/c bajo A, y todo su volumen acumulado sube a la linea de A.
/// Tambien las reglas de la revision: I1 (el arbol en compartido antes de resolver) e I2 (la tanda por fecha de
/// alta, el patrocinador pendiente primero).
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerRaizProvisionalTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>
    /// P (raiz) → A por la izquierda. B es raiz provisional: C bajo B y D bajo C (tres niveles). Al colocar a B
    /// (patrocinado por A) sube con su equipo bajo A, y la pierna de A y la de P suben exactamente lo acumulado.
    /// </summary>
    [SqlServerFact]
    public async Task Colocar_una_raiz_provisional_mueve_su_equipo_intacto_y_sube_su_volumen()
    {
        var x = BinarioDb.Prefijo("PRV");
        try
        {
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left));
                await BinarioDb.SembrarPerfilAsync(db, $"{x}A", $"{x}P");
                await BinarioDb.SembrarPerfilAsync(db, $"{x}B", $"{x}A");
                await BinarioDb.SembrarPerfilAsync(db, $"{x}C", $"{x}B");
                await BinarioDb.SembrarPerfilAsync(db, $"{x}D", $"{x}C");
                await BinarioDb.PuntosAsync(db, $"{x}B", 10);
                await BinarioDb.PuntosAsync(db, $"{x}C", 20);
                await BinarioDb.PuntosAsync(db, $"{x}D", 40);
            }

            await using (var db = SqlServerReportes.Db())
            {
                var motor = new PlacementService(db, new Reloj());
                // B no esta colocado y coloca a C: B se crea como raiz provisional; D va bajo C.
                (await motor.PlaceAsync($"{x}C", $"{x}B")).IsSuccess.Should().BeTrue();
                (await motor.PlaceAsync($"{x}D", $"{x}C")).IsSuccess.Should().BeTrue();
            }

            await using (var db = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(db);   // Plan Q: las colocaciones solo encolan
                var b = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}B");
                b.ParentMemberId.Should().BeNull("B es raiz provisional");
                b.LeftLegPoints.Should().Be(60, "los puntos de C y D cuentan para B");
                (await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}A")).LeftLegPoints
                    .Should().Be(0, "los de B todavia no suben a A");

                var r = await new PlacementService(db, new Reloj()).PlaceAsync($"{x}B", $"{x}A");
                r.IsSuccess.Should().BeTrue(r.Error);
                r.Value!.AlreadyPlaced.Should().BeFalse();
            }

            await using (var lectura = SqlServerReportes.Db())
            {
                await LegPointMaintenance.DrainAsync(lectura);   // Plan Q: el +T de B esta en la cola
                var nodos = await lectura.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(x)).ToDictionaryAsync(d => d.MemberId[x.Length..]);
                // Forma intacta, bajo A por la regla a (A no tenia hijo izquierdo).
                (nodos["B"].ParentMemberId, nodos["B"].Side).Should().Be(($"{x}A", TreeSide.Left));
                (nodos["C"].ParentMemberId, nodos["C"].Side).Should().Be(($"{x}B", TreeSide.Left));
                (nodos["D"].ParentMemberId, nodos["D"].Side).Should().Be(($"{x}C", TreeSide.Left));
                // Clave y ruta coherentes en todo el subarbol movido.
                foreach (var n in new[] { "B", "C", "D" })
                {
                    var padre = nodos[nodos[n].ParentMemberId![x.Length..]];
                    nodos[n].BinaryPath.Should().Equal(DualBinaryPath.Child(padre.BinaryPath!, nodos[n].Side));
                    nodos[n].Depth.Should().Be(padre.Depth + 1);
                    nodos[n].HierarchyPath.Should().Be(padre.HierarchyPath + nodos[n].MemberId + "/");
                }
                // El volumen acumulado (10 + 20 + 40 = 70) sube a A y a P por la pierna izquierda, con sus 3 nodos.
                nodos["A"].LeftLegPoints.Should().Be(70);
                nodos["A"].LeftLegCount.Should().Be(3);
                nodos["P"].LeftLegPoints.Should().Be(70);
                nodos["P"].LeftLegCount.Should().Be(4);
                (await lectura.PlacementLogs.CountAsync(l => l.MemberId == $"{x}B" && l.Reason!.Contains("provisional root"))).Should().Be(1);
            }
        }
        finally
        {
            await BinarioDb.LimpiarAsync(x);
        }
    }

    [SqlServerFact]
    public async Task Una_raiz_del_sistema_no_se_coloca_y_un_nodo_con_padre_ya_esta_colocado()
    {
        var x = BinarioDb.Prefijo("PRS");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left), ($"{x}R", null, TreeSide.Left));
        await BinarioDb.SembrarPerfilAsync(db, $"{x}R", null);   // raiz del sistema: sin patrocinador
        var motor = new PlacementService(db, new Reloj());

        (await motor.PlaceAsync($"{x}R", $"{x}P")).ErrorCode.Should().Be("ROOT_CANNOT_BE_PLACED");
        var r = await motor.PlaceAsync($"{x}A", $"{x}P");
        r.IsSuccess.Should().BeTrue();
        r.Value!.AlreadyPlaced.Should().BeTrue();
        await tx.RollbackAsync();
    }

    /// <summary>
    /// Mientras una raiz provisional con equipo se mueve (arbol en exclusivo), llegan colocaciones al mismo
    /// patrocinador: esperan, ninguna repite hueco y las claves y rutas quedan coherentes.
    /// </summary>
    [SqlServerFact]
    public async Task Una_colocacion_concurrente_durante_el_movimiento_no_repite_hueco()
    {
        var x = BinarioDb.Prefijo("PRC");
        try
        {
            await BinarioDb.LimpiarAsync(x);
            await using (var db = SqlServerReportes.Db())
            {
                await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left));
                await BinarioDb.SembrarPerfilAsync(db, $"{x}B", $"{x}A");
                foreach (var i in Enumerable.Range(1, 8)) await BinarioDb.SembrarPerfilAsync(db, $"{x}C{i}", $"{x}B");
            }
            await using (var db = SqlServerReportes.Db())
            {
                var motor = new PlacementService(db, new Reloj());
                foreach (var i in Enumerable.Range(1, 8)) (await motor.PlaceAsync($"{x}C{i}", $"{x}B")).IsSuccess.Should().BeTrue();
            }

            var tareas = new List<Task<MLMConquerorGlobalEdition.SharedKernel.Result<PlacementOutcome>>>
            {
                Task.Run(async () => { await using var db = SqlServerReportes.Db(); return await new PlacementService(db, new Reloj()).PlaceAsync($"{x}B", $"{x}A"); })
            };
            tareas.AddRange(Enumerable.Range(1, 10).Select(i => Task.Run(async () =>
            {
                await using var db = SqlServerReportes.Db();
                return await new PlacementService(db, new Reloj()).PlaceAsync($"{x}N{i:00}", $"{x}A");
            })));
            var resultados = await Task.WhenAll(tareas);

            resultados.Should().OnlyContain(r => r.IsSuccess, string.Join("; ", resultados.Where(r => !r.IsSuccess).Select(r => r.Error)));
            await using var lectura = SqlServerReportes.Db();
            (await BinarioDb.HuecosRepetidosAsync(lectura, x)).Should().Be(0);
            await LegPointMaintenance.DrainAsync(lectura);   // Plan Q: las colocaciones solo encolan
            var nodos = await lectura.DualTeamTree.AsNoTracking().Where(d => d.MemberId.StartsWith(x)).ToListAsync();
            nodos.Should().HaveCount(2 + 1 + 8 + 10);
            var porId = nodos.ToDictionary(n => n.MemberId);
            foreach (var n in nodos.Where(n => n.ParentMemberId is not null))
            {
                var p = porId[n.ParentMemberId!];
                n.BinaryPath.Should().Equal(DualBinaryPath.Child(p.BinaryPath!, n.Side));
                n.HierarchyPath.Should().Be(p.HierarchyPath + n.MemberId + "/");
            }
            porId[$"{x}P"].LeftLegCount.Should().Be(nodos.Count - 1);
        }
        finally
        {
            await BinarioDb.LimpiarAsync(x);
        }
    }

    /// <summary>
    /// Revision I1: una colocacion que llega mientras se retira el nodo que seria su destino espera (el arbol en
    /// compartido antes de resolver) y resuelve despues de la retirada: no cuelga de un nodo borrado.
    /// </summary>
    [SqlServerFact]
    public async Task Una_colocacion_durante_una_retirada_resuelve_despues_y_no_deja_huerfanos()
    {
        var x = BinarioDb.Prefijo("PRI");
        try
        {
            await using (var db = SqlServerReportes.Db())
                await BinarioDb.SembrarAsync(db, ($"{x}S", null, TreeSide.Left), ($"{x}T", $"{x}S", TreeSide.Left));

            await using var retirada = SqlServerReportes.Db();
            await using var txRetirada = await retirada.Database.BeginTransactionAsync();
            await DualTeamGate.EnterTreeExclusiveAsync(retirada);

            var colocacion = Task.Run(async () =>
            {
                await using var db = SqlServerReportes.Db();
                return await new PlacementService(db, new Reloj()).PlaceAsync($"{x}X", $"{x}S");
            });
            await Task.Delay(1000);
            colocacion.IsCompleted.Should().BeFalse("la colocacion espera al arbol antes de resolver");

            (await DualTeamNodes.RemoveAsync(retirada, $"{x}T", DualTeamChildrenOnRemoval.Roots, DateTime.Now, "plan-p-prueba")).Should().BeTrue();
            await txRetirada.CommitAsync();

            var r = await colocacion;
            r.IsSuccess.Should().BeTrue(r.Error);
            (r.Value!.ParentMemberId, r.Value.Side).Should().Be(($"{x}S", TreeSide.Left), "T ya no existe: S vuelve a tener libre la izquierda");
            await using var lectura = SqlServerReportes.Db();
            (await lectura.DualTeamTree.CountAsync(d => d.MemberId.StartsWith(x) && d.ParentMemberId != null
                && !lectura.DualTeamTree.Any(p => p.MemberId == d.ParentMemberId))).Should().Be(0, "ningun huerfano");
        }
        finally
        {
            await BinarioDb.LimpiarAsync(x);
        }
    }

    /// <summary>
    /// Revision I2: en una tanda con B (patrocinado por A, que ya esta) y C (patrocinado por B), B se coloca antes
    /// aunque venga despues en la lista: C cae bajo B sin crear a B como raiz provisional.
    /// </summary>
    [SqlServerFact]
    public async Task La_tanda_coloca_antes_al_patrocinador_pendiente()
    {
        var x = BinarioDb.Prefijo("PRT");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}A", null, TreeSide.Left));
        await BinarioDb.SembrarPerfilAsync(db, $"{x}B", $"{x}A");
        await BinarioDb.SembrarPerfilAsync(db, $"{x}C", $"{x}B");

        var r = await new PlacementService(db, new Reloj()).PlaceBulkAsync(new[] { ($"{x}C", $"{x}B"), ($"{x}B", $"{x}A") });

        r.Value!.Placed.Should().Be(2);
        var b = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}B");
        var c = await db.DualTeamTree.AsNoTracking().SingleAsync(d => d.MemberId == $"{x}C");
        b.ParentMemberId.Should().Be($"{x}A");
        c.ParentMemberId.Should().Be($"{x}B");
        (await db.PlacementLogs.CountAsync(l => l.MemberId == $"{x}B" && l.Reason!.Contains("provisional root")))
            .Should().Be(0, "B no llego a ser raiz provisional");
        await tx.RollbackAsync();
    }

    /// <summary>Decision de Nick: una raiz provisional con equipo cuenta como pendiente para la colocacion automatica.</summary>
    [SqlServerFact]
    public async Task Una_raiz_provisional_con_equipo_esta_pendiente_si_su_patrocinador_esta_colocado()
    {
        var x = BinarioDb.Prefijo("PRJ");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}A", null, TreeSide.Left), ($"{x}B", null, TreeSide.Left), ($"{x}C", $"{x}B", TreeSide.Left),
            ($"{x}Z", null, TreeSide.Left));
        await BinarioDb.SembrarPerfilAsync(db, $"{x}B", $"{x}A");    // patrocinador colocado: pendiente
        await BinarioDb.SembrarPerfilAsync(db, $"{x}C", $"{x}B");    // ya tiene padre: no
        await BinarioDb.SembrarPerfilAsync(db, $"{x}Z", $"{x}NOESTA"); // patrocinador sin nodo: espera
        await BinarioDb.SembrarPerfilAsync(db, $"{x}N", $"{x}A");    // sin nodo: pendiente

        var pendientes = await PendingPlacements.Query(db).Where(p => p.MemberId.StartsWith(x)).Select(p => p.MemberId).ToListAsync();

        pendientes.Should().BeEquivalentTo($"{x}B", $"{x}N");
        await tx.RollbackAsync();
    }
}
