using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Repository.Queries;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Rendimiento;

/// <summary>
/// Plan Q (visualizador del admin): lo que la migracion IndiceDelHistorialDeRangos deja en la base, el rango vigente y el
/// maximo de MemberRankQueries en SQL Server (con el desempate por Id) y su plan: una busqueda por miembro en el indice y
/// la lista de ids en un solo parametro.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerHistorialDeRangosTests
{
    [SqlServerFact]
    public async Task La_columna_queda_acotada_y_el_indice_existe_con_la_fecha_descendente_y_el_rango_incluido()
    {
        await using var db = SqlServerReportes.Db();

        (await db.Database.SqlQuery<int>($"""
            SELECT CAST(max_length AS int) AS [Value] FROM sys.columns
            WHERE object_id = OBJECT_ID('MemberRankHistories') AND name = 'MemberId'
            """).SingleAsync()).Should().Be(900);

        var columnas = await db.Database.SqlQuery<string>($"""
            SELECT CONCAT(c.name, ':', ic.key_ordinal, ':', ic.is_descending_key, ':', ic.is_included_column) AS [Value]
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID('MemberRankHistories') AND i.name = 'IX_MemberRankHistories_MemberId_AchievedAt'
            """).ToListAsync();
        columnas.Should().BeEquivalentTo("MemberId:1:0:0", "AchievedAt:2:1:0", "RankDefinitionId:0:0:1");
    }

    [SqlServerFact]
    public async Task El_vigente_y_el_maximo_con_el_desempate_por_Id_en_SQL_Server()
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var (a, b, c) = (SqlServerReportes.NuevoId("RA"), SqlServerReportes.NuevoId("RB"), SqlServerReportes.NuevoId("RC"));
        HistorialDeRangosTests.Sembrar(db, a, b, c, plata: 1, oro: 2, platino: 3);
        await db.SaveChangesAsync();

        var vigentes = await db.GetCurrentRanksAsync(new[] { a, b, c });
        var maximos = await db.GetLifetimeRanksAsync(new[] { a, b, c });

        vigentes[a].RankDefinitionId.Should().Be(2);
        vigentes[b].RankDefinitionId.Should().Be(1, "empate de fecha: gana el Id menor");
        vigentes[c].RankDefinitionId.Should().Be(2);
        (await db.GetCurrentRankAsync(b))!.RankDefinitionId.Should().Be(1);
        maximos[a].RankDefinitionId.Should().Be(3);
        maximos[b].RankDefinitionId.Should().Be(2);
        (await db.CurrentRankRows().Where(r => r.MemberId == a || r.MemberId == b).Select(r => r.Id).ToListAsync())
            .Should().BeEquivalentTo(a + "-3", b + "-1");
        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task El_vigente_de_un_nivel_del_binario_busca_por_miembro_y_manda_la_lista_en_un_parametro()
    {
        var cap = new CapturadorDeSql();
        await using var db = cap.Db();
        var ids = await db.MemberRankHistories.AsNoTracking().Select(r => r.MemberId).Distinct()
            .OrderBy(m => m).Take(127).ToListAsync();

        var plan = await PlanDeEjecucion.CompiladoAsync(db, db.CurrentRanks(ids));
        var maximo = await PlanDeEjecucion.CompiladoAsync(db, db.LifetimeRanks(ids));
        cap.Limpiar();
        await db.GetCurrentRanksAsync(ids);
        await db.GetLifetimeRanksAsync(ids);

        PlanDeEjecucion.Recorridos(plan, "MemberRankHistories").Should().BeEmpty();
        plan.Should().Contain("IX_MemberRankHistories_MemberId_AchievedAt").And.Contain(PlanDeEjecucion.Seek);
        PlanDeEjecucion.Recorridos(maximo, "MemberRankHistories").Should().BeEmpty();
        cap.Textos.Should().HaveCount(2).And.OnlyContain(t => t.Contains("OPENJSON"));
        cap.Valores.Should().HaveCount(2, "un parametro JSON por consulta, no uno por id");
    }
}
