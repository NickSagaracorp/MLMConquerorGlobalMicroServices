using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Services.Teams;
using Microsoft.EntityFrameworkCore;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Plan P, Tarea 1: los escenarios de diagnostico-huecos-binario.md §1.2 (reproducidos en el scratchpad
/// con 2 hijos en el mismo hueco), escritos esperando UN hijo. Cada prueba siembra con su prefijo dentro
/// de una transaccion que se deshace: PlacementService trabaja dentro de ella con puntos de guardado.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerColocacionBinarioTests
{
    private sealed class Reloj : MLMConquerorGlobalEdition.SharedKernel.Interfaces.IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
        public DateTime UtcNow => DateTime.UtcNow;
    }

    // Fallo 1: P ya tiene hijo izquierdo y llegan dos patrocinados de P en la misma tanda. El primero va a
    // la derecha; el segundo, con la cola sin resolver, caia en P-izquierda.
    [SqlServerFact]
    public Task Bulk_dos_patrocinados_con_el_izquierdo_ocupado_no_repiten_hueco() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        var x = BinarioDb.Prefijo("PP1");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left));

        var r = await new PlacementService(db, new Reloj()).PlaceBulkAsync(new[] { ($"{x}A", $"{x}P"), ($"{x}B", $"{x}P") });

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.Placed.Should().Be(2);
        (await BinarioDb.HijosAsync(db, $"{x}P", TreeSide.Left)).Should().Be(1);
        (await BinarioDb.HijosAsync(db, $"{x}P", TreeSide.Right)).Should().Be(1);
        (await BinarioDb.HuecosRepetidosAsync(db, x)).Should().Be(0);
        await tx.RollbackAsync();
    });

    // Fallo 3: el derrame de P cae bajo L-izquierda y despues llega un patrocinado directo de L. El estado
    // en memoria de L no veia lo que la tanda habia anadido.
    [SqlServerFact]
    public Task Bulk_derrame_y_alta_directa_bajo_el_mismo_nodo_no_repiten_hueco() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        var x = BinarioDb.Prefijo("PP3");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left),
            ($"{x}L", $"{x}P", TreeSide.Left), ($"{x}R", $"{x}P", TreeSide.Right));

        var r = await new PlacementService(db, new Reloj()).PlaceBulkAsync(new[] { ($"{x}Y", $"{x}P"), ($"{x}Z", $"{x}L") });

        r.IsSuccess.Should().BeTrue(r.Error);
        r.Value!.Placed.Should().Be(2);
        (await BinarioDb.HijosAsync(db, $"{x}L", TreeSide.Left)).Should().Be(1);
        (await BinarioDb.HuecosRepetidosAsync(db, x)).Should().Be(0);
        await tx.RollbackAsync();
    });

    // Fallo 2: la cola cacheada (L) ya tiene hijo izquierdo; la ruta masiva fijaba deepestNodeHasLeftChild
    // en false y la volvia a llenar por la izquierda. Ahora recibe el derecho.
    [SqlServerFact]
    public Task Bulk_cola_cacheada_con_hijo_izquierdo_recibe_el_derecho() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        var x = BinarioDb.Prefijo("PP2");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left),
            ($"{x}R", $"{x}P", TreeSide.Right), ($"{x}LL", $"{x}L", TreeSide.Left));
        db.DualTeamLegFrontiers.Add(new DualTeamLegFrontier
        {
            SponsorMemberId = $"{x}P", PreferredSide = TreeSide.Left, DeepestMemberId = $"{x}L", DeepestDepth = 3,
            CreationDate = DateTime.Now, CreatedBy = "plan-p-prueba", LastUpdateDate = DateTime.Now
        });
        await db.SaveChangesAsync();

        var r = await new PlacementService(db, new Reloj()).PlaceBulkAsync(new[] { ($"{x}Q", $"{x}P") });

        r.IsSuccess.Should().BeTrue(r.Error);
        (await BinarioDb.HijosAsync(db, $"{x}L", TreeSide.Left)).Should().Be(1);
        var q = await db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == $"{x}Q");
        (q.ParentMemberId, q.Side).Should().Be(($"{x}L", TreeSide.Right));
        await tx.RollbackAsync();
    });

    // Control del diagnostico: sin cache, la busqueda de la cola acierta (la hoja mas profunda, LL).
    [SqlServerFact]
    public Task Bulk_sin_cache_la_cola_es_la_hoja_mas_profunda() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        var x = BinarioDb.Prefijo("PP4");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}L", $"{x}P", TreeSide.Left),
            ($"{x}R", $"{x}P", TreeSide.Right), ($"{x}LL", $"{x}L", TreeSide.Left));

        var r = await new PlacementService(db, new Reloj()).PlaceBulkAsync(new[] { ($"{x}Q", $"{x}P") });

        r.IsSuccess.Should().BeTrue(r.Error);
        var q = await db.DualTeamTree.AsNoTracking().FirstAsync(d => d.MemberId == $"{x}Q");
        (q.ParentMemberId, q.Side).Should().Be(($"{x}LL", TreeSide.Left));
        await tx.RollbackAsync();
    });
}
