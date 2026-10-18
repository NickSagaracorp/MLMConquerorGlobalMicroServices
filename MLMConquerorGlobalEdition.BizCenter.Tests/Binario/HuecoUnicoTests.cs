using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Tree;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Binario;

/// <summary>Plan P, Tarea 5: un hijo vivo por hueco (ParentMemberId, Side), la red de DualTeamGate.</summary>
public class HuecoUnicoModeloTests
{
    [Fact]
    public void El_modelo_declara_el_indice_unico_filtrado_de_hueco()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=.;Database=NoSeAbre;Integrated Security=true").Options);
        var indice = db.Model.FindEntityType(typeof(DualTeamEntity))!.GetIndexes()
            .SingleOrDefault(i => i.GetDatabaseName() == DualTeamSlotTaken.IndexName);

        indice.Should().NotBeNull();
        indice!.IsUnique.Should().BeTrue();
        indice.Properties.Select(p => p.Name).Should().Equal("ParentMemberId", "Side");
        indice.GetFilter().Should().Be("[IsDeleted] = 0 AND [ParentMemberId] IS NOT NULL");
    }
}

[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerHuecoUnicoTests
{
    [SqlServerFact]
    public async Task Dos_hijos_vivos_en_el_mismo_hueco_violan_el_indice()
    {
        var x = BinarioDb.Prefijo("PPU");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left));

        db.DualTeamTree.Add(new DualTeamEntity
        {
            MemberId = $"{x}B", ParentMemberId = $"{x}P", Side = TreeSide.Left, HierarchyPath = $"/{x}P/{x}B/",
            CreationDate = DateTime.Now, CreatedBy = "plan-p-prueba", LastUpdateDate = DateTime.Now
        });
        var act = () => db.SaveChangesAsync();

        var ex = (await act.Should().ThrowAsync<DbUpdateException>()).Which;
        DualTeamSlotTaken.Is(ex).Should().BeTrue(ex.InnerException?.Message);
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task Un_hijo_borrado_no_ocupa_el_hueco()
    {
        var x = BinarioDb.Prefijo("PPU");
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        await BinarioDb.SembrarAsync(db, ($"{x}P", null, TreeSide.Left), ($"{x}A", $"{x}P", TreeSide.Left));
        db.DualTeamTree.Add(new DualTeamEntity
        {
            MemberId = $"{x}B", ParentMemberId = $"{x}P", Side = TreeSide.Left, HierarchyPath = $"/{x}P/{x}B/",
            IsDeleted = true, CreationDate = DateTime.Now, CreatedBy = "plan-p-prueba", LastUpdateDate = DateTime.Now
        });

        await db.SaveChangesAsync();   // no lanza
        await tx.RollbackAsync();
    }
}
