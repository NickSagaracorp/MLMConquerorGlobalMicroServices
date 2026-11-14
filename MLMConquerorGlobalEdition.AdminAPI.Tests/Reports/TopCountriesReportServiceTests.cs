using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Reports;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class TopCountriesReportServiceTests
{
    private static readonly DateTime Oct14 = new(2026, 10, 14);
    private static readonly DateTime Nov13 = new(2026, 11, 13);   // 30 días después de Oct14

    private const MemberType Amb = MemberType.Ambassador;
    private const MemberType Cli = MemberType.ExternalMember;

    private static AppDbContext Vacia() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static void Foto(AppDbContext db, DateTime dia, string pais, MemberType tipo, MemberAccountStatus estado, int n) =>
        db.MemberCountrySnapshots.Add(new MemberCountrySnapshot
        {
            SnapshotDate = dia, Country = pais, MemberType = tipo, Status = estado, MemberCount = n,
            CreationDate = dia, CreatedBy = "seed"
        });

    private static AppDbContext Base()
    {
        var db = Vacia();
        Foto(db, Oct14, "US", Amb, MemberAccountStatus.Active, 10);
        Foto(db, Oct14, "US", Cli, MemberAccountStatus.Active, 4);
        Foto(db, Oct14, "Peru", Amb, MemberAccountStatus.Active, 3);
        Foto(db, Nov13, "US", Amb, MemberAccountStatus.Active, 12);
        Foto(db, Nov13, "US", Cli, MemberAccountStatus.Active, 5);
        Foto(db, Nov13, "US", Amb, MemberAccountStatus.Suspended, 2);
        Foto(db, Nov13, "Peru", Amb, MemberAccountStatus.Active, 1);
        Foto(db, Nov13, "Chile", Amb, MemberAccountStatus.Active, 6);
        Foto(db, Nov13, "Chile", Amb, MemberAccountStatus.Terminated, 1);
        db.SaveChanges();
        return db;
    }

    private static TopCountriesReportService Servicio(AppDbContext db) => new(db);

    [Fact]
    public async Task Tabla_por_pais_con_cambio_contra_30_dias_antes()
    {
        using var db = Base();

        var t = await Servicio(db).GetTableAsync(Nov13, null);

        (t.SnapshotDate, t.ComparedWith).Should().Be(((DateTime?)Nov13, (DateTime?)Oct14));
        t.Rows.Select(r => (r.Country, r.Total, r.Ambassadors, r.Customers, r.Change)).Should().Equal(
            ("US", 19, 14, 5, (int?)5),
            ("Chile", 7, 7, 0, (int?)7),
            ("Peru", 1, 1, 0, (int?)-2));
    }

    [Fact]
    public async Task El_filtro_de_estados_cuenta_solo_esos()
    {
        using var db = Base();

        var t = await Servicio(db).GetTableAsync(Nov13, new[] { MemberAccountStatus.Active });

        t.Rows.Select(r => (r.Country, r.Total, r.Change)).Should().Equal(
            ("US", 17, (int?)3), ("Chile", 6, (int?)6), ("Peru", 1, (int?)-2));
    }

    [Fact]
    public async Task Una_fecha_sin_foto_usa_la_ultima_anterior()
    {
        using var db = Base();

        (await Servicio(db).GetTableAsync(new DateTime(2026, 11, 14), null)).SnapshotDate.Should().Be(Nov13);
    }

    [Fact]
    public async Task Sin_foto_de_30_dias_antes_el_cambio_queda_vacio()
    {
        using var db = Vacia();
        Foto(db, Nov13, "US", Amb, MemberAccountStatus.Active, 5);
        await db.SaveChangesAsync();

        var t = await Servicio(db).GetTableAsync(Nov13, null);

        t.ComparedWith.Should().BeNull();
        t.Rows.Single().Change.Should().BeNull();
    }

    [Fact]
    public async Task Compara_con_la_ultima_foto_hasta_30_dias_antes()
    {
        using var db = Vacia();
        Foto(db, new DateTime(2026, 10, 10), "US", Amb, MemberAccountStatus.Active, 1);
        Foto(db, Oct14, "US", Amb, MemberAccountStatus.Active, 2);
        Foto(db, new DateTime(2026, 10, 20), "US", Amb, MemberAccountStatus.Active, 100);
        Foto(db, Nov13, "US", Amb, MemberAccountStatus.Active, 10);
        await db.SaveChangesAsync();

        var t = await Servicio(db).GetTableAsync(Nov13, null);

        t.ComparedWith.Should().Be(Oct14);
        t.Rows.Single().Change.Should().Be(8);
    }

    [Fact]
    public async Task Sin_ninguna_foto_la_tabla_esta_vacia()
    {
        using var db = Vacia();

        var t = await Servicio(db).GetTableAsync(Nov13, null);

        t.SnapshotDate.Should().BeNull();
        t.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Grafica_mensual_toma_la_ultima_foto_de_cada_mes()
    {
        using var db = Base();

        var puntos = await Servicio(db).GetChartAsync(
            ReportPeriods.Months(new DateTime(2026, 9, 1), new DateTime(2026, 11, 30)), null);

        puntos.Select(p => (p.Label, p.SnapshotDate, p.Total)).Should().Equal(
            ("Sep 2026", (DateTime?)null, (int?)null),
            ("Oct 2026", (DateTime?)Oct14, (int?)17),
            ("Nov 2026", (DateTime?)Nov13, (int?)27));
    }

    [Fact]
    public async Task Grafica_con_estados_sin_filas_en_la_foto_da_cero()
    {
        using var db = Base();

        var puntos = await Servicio(db).GetChartAsync(
            ReportPeriods.Months(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30)), new[] { MemberAccountStatus.Pending });

        (puntos.Single().SnapshotDate, puntos.Single().Total).Should().Be(((DateTime?)Nov13, (int?)0));
    }

    [Fact]
    public async Task Grafica_semanal_pone_la_foto_en_su_semana()
    {
        using var db = Base();

        var puntos = await Servicio(db).GetChartAsync(ReportPeriods.WeeksOfMonth(2026, 10), null);

        puntos.Select(p => p.Label).Should().Equal("Oct 1-4", "Oct 5-11", "Oct 12-18", "Oct 19-25", "Oct 26-31");
        puntos.Single(p => p.Total is not null).Should().BeEquivalentTo(new { Label = "Oct 12-18", Total = (int?)17 });
    }
}
