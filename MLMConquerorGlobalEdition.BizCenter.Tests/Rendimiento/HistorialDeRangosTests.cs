using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan Q (visualizador del admin): MemberRankHistories acotada e indexada, y el rango vigente y el maximo de cada miembro
/// decididos en un solo sitio (MemberRankQueries), con el desempate por Id menor.
/// </summary>
public class HistorialDeRangosTests
{
    private static readonly DateTime Base = ReportesDb.Base;

    private static IEntityType Tipo()
    {
        using var db = ReportesDb.Nueva();
        return db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(MemberRankHistory))!;
    }

    [Fact]
    public void MemberId_acotado_como_los_ids_de_los_arboles()
        => Tipo().FindProperty("MemberId")!.GetMaxLength().Should().Be(450);

    [Fact]
    public void Indice_por_miembro_y_fecha_descendente_con_el_rango_incluido()
    {
        var i = Tipo().GetIndexes().Single(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { "MemberId", "AchievedAt" }));
        i.IsDescending.Should().Equal(false, true);
        i.GetIncludeProperties().Should().BeEquivalentTo("RankDefinitionId");
    }

    /// <summary>Tres rangos (SortOrder 1, 2, 3) y un historial con un empate de fecha y un descenso.</summary>
    internal static void Sembrar(AppDbContext db, string a, string b, string c, int plata, int oro, int platino)
    {
        Fila(db, a + "-1", a, plata, Base.AddDays(-30));
        Fila(db, a + "-2", a, platino, Base.AddDays(-20));
        Fila(db, a + "-3", a, oro, Base.AddDays(-10));          // descendio: vigente Oro, maximo Platino
        Fila(db, b + "-2", b, oro, Base.AddDays(-5));           // empate de fecha: gana el Id menor (b-1)
        Fila(db, b + "-1", b, plata, Base.AddDays(-5));
        Fila(db, c + "-1", c, oro, Base.AddDays(-1));
    }

    private static void Fila(AppDbContext db, string id, string memberId, int rango, DateTime fecha) =>
        db.MemberRankHistories.Add(new MemberRankHistory
        {
            Id = id, MemberId = memberId, RankDefinitionId = rango, AchievedAt = fecha,
            CreationDate = fecha, LastUpdateDate = fecha, CreatedBy = "prueba"
        });

    private static AppDbContext ConRangos()
    {
        var db = ReportesDb.Nueva();
        db.RankDefinitions.AddRange(
            new RankDefinition { Id = 1, Name = "Silver", SortOrder = 1, CreatedBy = "prueba" },
            new RankDefinition { Id = 2, Name = "Gold", SortOrder = 2, CreatedBy = "prueba" },
            new RankDefinition { Id = 3, Name = "Platinum", SortOrder = 3, CreatedBy = "prueba" });
        return db;
    }

    [Fact]
    public async Task El_vigente_es_la_fila_mas_reciente_y_el_empate_lo_gana_el_Id_menor()
    {
        await using var db = ConRangos();
        Sembrar(db, "A", "B", "C", 1, 2, 3);
        await db.SaveChangesAsync();

        var vigentes = await db.GetCurrentRanksAsync(new[] { "A", "B", "SIN-HISTORIAL", "A" });

        vigentes.Keys.Should().BeEquivalentTo("A", "B");
        vigentes["A"].Should().Be(new MemberRankEntry("A", 2, "Gold", 2, Base.AddDays(-10)));
        vigentes["B"].Should().Be(new MemberRankEntry("B", 1, "Silver", 1, Base.AddDays(-5)));
        (await db.GetCurrentRankAsync("B"))!.RankDefinitionId.Should().Be(1);
        (await db.GetCurrentRankAsync("SIN-HISTORIAL")).Should().BeNull();
        (await db.CurrentRankRows().Select(r => r.Id).ToListAsync()).Should().BeEquivalentTo("A-3", "B-1", "C-1");
    }

    [Fact]
    public async Task El_maximo_es_el_de_mayor_orden()
    {
        await using var db = ConRangos();
        Sembrar(db, "A", "B", "C", 1, 2, 3);
        await db.SaveChangesAsync();

        var maximos = await db.GetLifetimeRanksAsync(new[] { "A", "B", "C" });

        maximos["A"].RankName.Should().Be("Platinum");
        maximos["B"].RankName.Should().Be("Gold");
        maximos["C"].RankName.Should().Be("Gold");
    }

    [Fact]
    public async Task Sin_ids_no_consulta()
    {
        await using var db = ConRangos();
        (await db.GetCurrentRanksAsync(Array.Empty<string>())).Should().BeEmpty();
        (await db.GetLifetimeRanksAsync(Array.Empty<string>())).Should().BeEmpty();
    }
}
