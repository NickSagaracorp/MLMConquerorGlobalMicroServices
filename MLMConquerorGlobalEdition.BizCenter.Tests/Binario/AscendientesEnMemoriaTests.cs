using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>
/// Revision del bloque 3: fuera de SQL Server el camino sube por ParentMemberId. Un ciclo de padres no lo cuelga y un
/// nodo que no esta debajo de la raiz da un camino vacio (no el camino hasta otra raiz).
/// </summary>
public class AscendientesEnMemoriaTests
{
    private static AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static DualTeamEntity Nodo(string id, string? padre, TreeSide lado = TreeSide.Left) =>
        new() { MemberId = id, ParentMemberId = padre, Side = lado, CreatedBy = "prueba", CreationDate = DateTime.Now };

    private static async Task<AppDbContext> SembrarAsync(params DualTeamEntity[] nodos)
    {
        var db = Db();
        db.AddRange(nodos);
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task Debajo_de_la_raiz_da_el_camino_de_arriba_abajo()
    {
        await using var db = await SembrarAsync(Nodo("R", null), Nodo("A", "R"), Nodo("B", "A", TreeSide.Right));
        var caminos = await DualAncestors.PathsBelowAsync(db, Nodo("R", null), new[] { Nodo("B", "A", TreeSide.Right) });
        caminos["B"].Should().Equal(new DualPathStep("A", TreeSide.Left), new DualPathStep("B", TreeSide.Right));
    }

    [Fact(Timeout = 10_000)]
    public async Task Un_ciclo_de_padres_no_cuelga_y_da_un_camino_vacio()
    {
        await using var db = await SembrarAsync(Nodo("R", null), Nodo("A", "B"), Nodo("B", "A"));
        var caminos = await DualAncestors.PathsBelowAsync(db, Nodo("R", null), new[] { Nodo("A", "B") });
        caminos["A"].Should().BeEmpty();
    }

    [Fact]
    public async Task Un_nodo_que_no_esta_debajo_de_la_raiz_da_un_camino_vacio()
    {
        await using var db = await SembrarAsync(Nodo("R", null), Nodo("S", null), Nodo("A", "S"));
        var caminos = await DualAncestors.PathsBelowAsync(db, Nodo("R", null), new[] { Nodo("A", "S") });
        caminos["A"].Should().BeEmpty();
    }
}
