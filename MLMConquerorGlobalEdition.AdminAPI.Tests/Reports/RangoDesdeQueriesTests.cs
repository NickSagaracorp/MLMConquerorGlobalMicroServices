using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>
/// Rango de por vida (el de mayor SortOrder, desde la PRIMERA vez que se alcanzó) y rango vigente (la fila más reciente)
/// de cada miembro con historial, para los reportes Lifetime Rank y Current Rank.
/// </summary>
public class RangoDesdeQueriesTests
{
    /// <summary>Base InMemory con el rango 0 (Lifestyle Consultant) y Silver, Gold y Platinum.</summary>
    internal static AppDbContext ConRangos()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.RankDefinitions.AddRange(
            new RankDefinition { Id = 20, Name = "Lifestyle Consultant", SortOrder = 0, CreatedBy = "seed" },
            new RankDefinition { Id = 1,  Name = "Silver",               SortOrder = 1, CreatedBy = "seed" },
            new RankDefinition { Id = 2,  Name = "Gold",                 SortOrder = 2, CreatedBy = "seed" },
            new RankDefinition { Id = 3,  Name = "Platinum",             SortOrder = 3, CreatedBy = "seed" });
        return db;
    }

    internal static void Rango(AppDbContext db, string memberId, int rankId, DateTime fecha) =>
        db.MemberRankHistories.Add(new MemberRankHistory
        {
            MemberId = memberId, RankDefinitionId = rankId, AchievedAt = fecha,
            CreationDate = fecha, LastUpdateDate = fecha, CreatedBy = "seed"
        });

    /// <summary>Silver, Gold, baja a Silver, vuelve a Gold y baja otra vez a Silver.</summary>
    private static async Task<AppDbContext> SubeYBajaAsync()
    {
        var db = ConRangos();
        Rango(db, "AMB-1", 1, new DateTime(2026, 1, 10));
        Rango(db, "AMB-1", 2, new DateTime(2026, 3, 1, 9, 0, 0));
        Rango(db, "AMB-1", 1, new DateTime(2026, 5, 1));
        Rango(db, "AMB-1", 2, new DateTime(2026, 6, 1));
        Rango(db, "AMB-1", 1, new DateTime(2026, 7, 1));
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task De_por_vida_es_el_mayor_rango_desde_la_primera_vez_que_se_alcanzo()
    {
        using var db = await SubeYBajaAsync();

        var r = await db.LifetimeRankSinceRows().SingleAsync();

        (r.MemberId, r.RankDefinitionId, r.SortOrder, r.Since)
            .Should().Be(("AMB-1", 2, 2, new DateTime(2026, 3, 1, 9, 0, 0)));
    }

    [Fact]
    public async Task Vigente_es_la_fila_mas_reciente_con_su_fecha()
    {
        using var db = await SubeYBajaAsync();

        var r = await db.CurrentRankSinceRows().SingleAsync();

        (r.MemberId, r.RankDefinitionId, r.SortOrder, r.Since)
            .Should().Be(("AMB-1", 1, 1, new DateTime(2026, 7, 1)));
    }

    [Fact]
    public async Task Una_fila_por_miembro_con_historial()
    {
        using var db = ConRangos();
        Rango(db, "AMB-1", 3, new DateTime(2026, 2, 1));
        Rango(db, "AMB-2", 20, new DateTime(2026, 2, 1));
        Rango(db, "AMB-2", 20, new DateTime(2026, 4, 1));
        await db.SaveChangesAsync();

        (await db.LifetimeRankSinceRows().Select(r => r.MemberId).ToListAsync()).Should().BeEquivalentTo("AMB-1", "AMB-2");
        (await db.CurrentRankSinceRows().Select(r => r.MemberId).ToListAsync()).Should().BeEquivalentTo("AMB-1", "AMB-2");
        (await db.LifetimeRankSinceRows().SingleAsync(r => r.MemberId == "AMB-2")).Since.Should().Be(new DateTime(2026, 2, 1));
    }
}
