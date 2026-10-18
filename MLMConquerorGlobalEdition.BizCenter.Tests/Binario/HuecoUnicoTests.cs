using Microsoft.Data.SqlClient;
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

/// <summary>
/// Se salta (con su motivo, no se da por buena) si la base no tiene aun el indice: en la base local de
/// desarrollo no se aplica hasta reparar los huecos con los servicios parados (Plan P, Tarea 3 y 5).
/// Se ejecuta contra una base reparada con MLM_TEST_SQLSERVER.
/// </summary>
public sealed class SqlServerConHuecoUnicoFactAttribute : FactAttribute
{
    public SqlServerConHuecoUnicoFactAttribute()
    {
        if (!SqlServerReportes.Reachable)
        {
            Skip = "SQL Server no alcanzable (define MLM_TEST_SQLSERVER para usar otra instancia).";
            return;
        }
        try
        {
            using var c = new SqlConnection(SqlServerReportes.ConnectionString);
            c.Open();
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM sys.indexes WHERE name = @n AND object_id = OBJECT_ID('DualTeamTree')", c);
            cmd.Parameters.AddWithValue("@n", DualTeamSlotTaken.IndexName);
            if ((int)cmd.ExecuteScalar()! == 0)
                Skip = $"{DualTeamSlotTaken.IndexName} no esta aplicado en esta base (se aplica tras la reparacion, Plan P Tarea 5).";
        }
        catch (SqlException ex)
        {
            Skip = "No se pudo comprobar el indice: " + ex.Message;
        }
    }
}

[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerHuecoUnicoTests
{
    [SqlServerConHuecoUnicoFact]
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

    [SqlServerConHuecoUnicoFact]
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
