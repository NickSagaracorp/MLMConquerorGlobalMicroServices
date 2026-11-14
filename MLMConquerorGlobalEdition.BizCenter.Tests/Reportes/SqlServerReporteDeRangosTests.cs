using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using Xunit.Abstractions;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;

/// <summary>
/// Lifetime Rank y Current Rank se traducen a SQL Server (la agregación por miembro, la antijoin del vigente y la unión
/// con los perfiles) y eligen lo mismo que en memoria. La segunda prueba escribe el SQL en la salida para medirlo.
/// </summary>
[Collection(SqlServerReportesCollection.Nombre)]
public class SqlServerReporteDeRangosTests
{
    private readonly ITestOutputHelper _salida;

    public SqlServerReporteDeRangosTests(ITestOutputHelper salida) => _salida = salida;

    private static void Rango(AppDbContext db, string memberId, int rankId, DateTime fecha) =>
        db.MemberRankHistories.Add(new MemberRankHistory
        {
            MemberId = memberId, RankDefinitionId = rankId, AchievedAt = fecha,
            CreationDate = fecha, LastUpdateDate = fecha, CreatedBy = "prueba"
        });

    [SqlServerFact]
    public Task Los_dos_modos_se_traducen_y_eligen_bien() => SqlServerReportes.ConReintentoAsync(async () =>
    {
        await using var db = SqlServerReportes.Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var yo = SqlServerReportes.NuevoId("RK");
        ReportesDb.Miembro(db, yo);
        Rango(db, yo, 1, new DateTime(2026, 1, 10));     // Silver
        Rango(db, yo, 2, new DateTime(2026, 3, 1));      // Gold, la primera vez
        Rango(db, yo, 1, new DateTime(2026, 5, 1));
        Rango(db, yo, 2, new DateTime(2026, 6, 1));
        Rango(db, yo, 1, new DateTime(2026, 7, 1));      // vigente: Silver
        await db.SaveChangesAsync();
        var servicio = new RankReportService(db, new MemoryCache(new MemoryCacheOptions()));
        var correo = $"{yo.ToLowerInvariant()}@prueba.test";

        var porVida = (await servicio.GetAllAsync(new RankReportFilter(RankReportMode.Lifetime, Search: correo))).Single();
        var vigente = (await servicio.GetAllAsync(new RankReportFilter(RankReportMode.Current, Search: correo))).Single();

        (porVida.RankDefinitionId, porVida.Since).Should().Be((2, new DateTime(2026, 3, 1)));
        (vigente.RankDefinitionId, vigente.Since).Should().Be((1, new DateTime(2026, 7, 1)));
        await tx.RollbackAsync();
    });

    [SqlServerFact]
    public void Las_consultas_se_traducen_a_SQL()
    {
        using var db = SqlServerReportes.Db();
        foreach (var modo in new[] { RankReportMode.Lifetime, RankReportMode.Current })
        {
            var sql = RankReportService.SourceQuery(db, modo).ToQueryString();
            sql.Should().Contain("[MemberProfiles]");
            _salida.WriteLine($"-- {modo}{Environment.NewLine}{sql}{Environment.NewLine}");
        }
    }
}
