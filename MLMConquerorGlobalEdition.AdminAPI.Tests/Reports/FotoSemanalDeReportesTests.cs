using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Rank;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Moq;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>
/// Foto semanal de Earnings 180 Days, Earnings Lifetime, Lifetime Rank y Current Rank: la escribe el job de los lunes con
/// el mismo cálculo que la pantalla, y la pantalla la lee cuando el corte (o el modo) tiene foto; si no, calcula al
/// momento como antes.
/// </summary>
[Collection(CacheDeReportesCollection.Nombre)]
public class FotoSemanalDeReportesTests
{
    private static readonly DateTime Domingo = new(2026, 11, 22);

    private static AppDbContext Base()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.RankDefinitions.AddRange(
            new RankDefinition { Id = 20, Name = "Lifestyle Consultant", SortOrder = 0, CreatedBy = "seed" },
            new RankDefinition { Id = 1,  Name = "Silver",               SortOrder = 1, CreatedBy = "seed" },
            new RankDefinition { Id = 2,  Name = "Gold",                 SortOrder = 2, CreatedBy = "seed" });
        Miembro(db, "AMB-1", "Ana", "Lopez", "ana@example.com", "Bulgaria");
        Miembro(db, "AMB-2", "Beto", "Diaz", "beto@example.com", "France");
        Miembro(db, "AMB-3", "Carla", "Ruiz", "carla@example.com", "Japan", MemberAccountStatus.Suspended);
        Comision(db, "AMB-1", 100m, Domingo.AddDays(-10));
        Comision(db, "AMB-2", 300m, Domingo.AddDays(-400));   // fuera de los 180 días
        Comision(db, "AMB-3", 50m, Domingo);                  // el domingo entero cuenta
        Comision(db, "AMB-1", 999m, Domingo.AddDays(1));      // después del corte: no cuenta
        Rango(db, "AMB-1", 1, Domingo.AddDays(-30));
        Rango(db, "AMB-1", 2, Domingo.AddDays(-5));
        Rango(db, "AMB-2", 1, Domingo.AddDays(-50));
        Rango(db, "AMB-3", 2, Domingo.AddDays(-60));
        Rango(db, "AMB-3", 1, Domingo.AddDays(-20));
        db.SaveChanges();
        return db;
    }

    private static void Miembro(AppDbContext db, string id, string nombre, string apellido, string email, string pais,
        MemberAccountStatus estado = MemberAccountStatus.Active) =>
        db.MemberProfiles.Add(new MemberProfile
        {
            Id = Guid.NewGuid().ToString(), MemberId = id, FirstName = nombre, LastName = apellido, Email = email,
            Country = pais, Status = estado, MemberType = MemberType.Ambassador, ReplicateSiteSlug = id.ToLowerInvariant(),
            CreatedBy = "seed", CreationDate = Domingo, LastUpdateDate = Domingo
        });

    private static void Comision(AppDbContext db, string beneficiario, decimal monto, DateTime fecha) =>
        db.CommissionEarnings.Add(new CommissionEarning
        {
            Id = Guid.NewGuid().ToString(), BeneficiaryMemberId = beneficiario, Amount = monto, EarnedDate = fecha,
            PaymentDate = fecha, Status = CommissionEarningStatus.Paid, CommissionTypeId = 1,
            CreatedBy = "seed", CreationDate = fecha, LastUpdateDate = fecha
        });

    private static void Rango(AppDbContext db, string id, int rango, DateTime fecha) =>
        db.MemberRankHistories.Add(new MemberRankHistory
        {
            MemberId = id, RankDefinitionId = rango, AchievedAt = fecha,
            CreationDate = fecha, LastUpdateDate = fecha, CreatedBy = "seed"
        });

    private static EarningsRankingService Ganancias(AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Domingo.AddDays(3));
        return new EarningsRankingService(db, new MemoryCache(new MemoryCacheOptions()), reloj.Object);
    }

    private static RankReportService Rangos(AppDbContext db) => new(db, new MemoryCache(new MemoryCacheOptions()));

    private static GridDataRequest Pagina(string? buscar = null) => new() { Page = 1, PageSize = 50, Search = buscar };

    [Theory]
    [InlineData("2026-11-23", "2026-11-22")]   // lunes: el domingo que acaba de cerrar
    [InlineData("2026-11-25", "2026-11-22")]   // miércoles: el mismo domingo
    [InlineData("2026-11-22", "2026-11-15")]   // domingo: su semana aún no cerró
    [InlineData("2026-12-01", "2026-11-29")]   // cambio de mes
    public void La_foto_es_del_ultimo_domingo_cerrado(string hoy, string domingo) =>
        ReportSnapshotWriter.UltimoDomingoCerrado(DateTime.Parse(hoy)).Should().Be(DateTime.Parse(domingo));

    [Fact]
    public async Task La_foto_guarda_lo_mismo_que_el_calculo_en_vivo()
    {
        using var db = Base();
        var enVivo180  = (await Ganancias(db).GetAllAsync(Domingo, 180, null)).ToList();
        var enVivoVida = (await Ganancias(db).GetAllAsync(Domingo, null, null)).ToList();
        var enVivoRango = (await Rangos(db).GetAllAsync(new RankReportFilter(RankReportMode.Lifetime))).ToList();

        var r = await new ReportSnapshotWriter(db).WriteAsync(Domingo);

        r.Should().Be(new ReportSnapshotResult(2, 3, 3, 3));
        (await Ganancias(db).GetAllAsync(Domingo, 180, null)).Should().BeEquivalentTo(enVivo180, o => o.WithStrictOrdering());
        (await Ganancias(db).GetAllAsync(Domingo, null, null)).Should().BeEquivalentTo(enVivoVida, o => o.WithStrictOrdering());
        (await Rangos(db).GetAllAsync(new RankReportFilter(RankReportMode.Lifetime)))
            .Should().BeEquivalentTo(enVivoRango, o => o.WithStrictOrdering());
        (await new ReportSnapshotWriter(db).ExistsAsync(Domingo)).Should().BeTrue();
    }

    [Fact]
    public async Task Con_foto_la_pantalla_la_lee_aunque_los_datos_hayan_cambiado()
    {
        using var db = Base();
        await new ReportSnapshotWriter(db).WriteAsync(Domingo);
        Comision(db, "AMB-3", 1_000m, Domingo.AddDays(-1));   // llega tarde: entra en la foto del lunes siguiente
        await db.SaveChangesAsync();

        var pagina = await Ganancias(db).GetPageAsync(Domingo, 180, Pagina());

        pagina.Items.Select(i => (i.MemberId, i.Total)).Should().Equal(("AMB-1", 100m), ("AMB-3", 50m));
        pagina.TotalCount.Should().Be(2);
        (await Ganancias(db).UltimaFotoAsync()).Should().Be(Domingo);
    }

    [Fact]
    public async Task Otro_corte_u_otra_ventana_se_calculan_al_momento()
    {
        using var db = Base();
        await new ReportSnapshotWriter(db).WriteAsync(Domingo);
        Comision(db, "AMB-3", 1_000m, Domingo.AddDays(-1));
        await db.SaveChangesAsync();

        (await Ganancias(db).GetAllAsync(Domingo, 90, null)).First().Total.Should().Be(1_050m);             // otra ventana
        (await Ganancias(db).GetAllAsync(Domingo.AddDays(-1), 180, null)).First().Total.Should().Be(1_000m); // otro corte
    }

    [Fact]
    public async Task La_busqueda_en_la_foto_no_distingue_mayusculas_y_conserva_el_rank()
    {
        using var db = Base();
        await new ReportSnapshotWriter(db).WriteAsync(Domingo);

        var pagina = await Ganancias(db).GetPageAsync(Domingo, null, Pagina("CARLA@"));

        pagina.Items.Should().ContainSingle().Which.Rank.Should().Be(3);
        pagina.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Los_rangos_salen_de_la_ultima_foto_con_sus_filtros()
    {
        using var db = Base();
        await new ReportSnapshotWriter(db).WriteAsync(Domingo);
        Rango(db, "AMB-2", 2, Domingo.AddDays(1));   // después de la foto: no se ve hasta la siguiente
        await db.SaveChangesAsync();

        var vigente = await Rangos(db).GetPageAsync(new RankReportFilter(RankReportMode.Current), Pagina());
        vigente.Items.Single(i => i.MemberId == "AMB-2").RankName.Should().Be("Silver");

        var suspendidos = await Rangos(db).GetPageAsync(new RankReportFilter(RankReportMode.Current,
            Statuses: new[] { MemberAccountStatus.Suspended }), Pagina());
        suspendidos.Items.Select(i => i.MemberId).Should().Equal("AMB-3");

        var porPais = await Rangos(db).GetPageAsync(new RankReportFilter(RankReportMode.Lifetime), Pagina("france"));
        porPais.Items.Select(i => i.MemberId).Should().Equal("AMB-2");
        (await Rangos(db).UltimaFotoAsync(RankReportMode.Current)).Should().Be(Domingo);
    }

    [Fact]
    public async Task Repetir_la_foto_la_reemplaza_y_se_guardan_doce_semanas()
    {
        using var db = Base();
        var escritor = new ReportSnapshotWriter(db);
        for (var i = 13; i >= 0; i--) await escritor.WriteAsync(Domingo.AddDays(-7 * i));
        await escritor.WriteAsync(Domingo);

        var fechas = await db.EarningsRankingSnapshots.Select(s => s.SnapshotDate).Distinct().ToListAsync();
        fechas.Should().HaveCount(ReportSnapshotWriter.FotosQueSeGuardan);
        fechas.Min().Should().Be(Domingo.AddDays(-7 * (ReportSnapshotWriter.FotosQueSeGuardan - 1)));
        (await db.EarningsRankingSnapshots.CountAsync(s => s.SnapshotDate == Domingo && s.WindowDays == 0)).Should().Be(3);
        (await db.RankStandingSnapshots.Select(s => s.SnapshotDate).Distinct().CountAsync())
            .Should().Be(ReportSnapshotWriter.FotosQueSeGuardan);
    }

    [Fact]
    public async Task Sin_foto_los_rangos_se_calculan_al_momento()
    {
        using var db = Base();
        (await Rangos(db).UltimaFotoAsync(RankReportMode.Lifetime)).Should().BeNull();
        (await Rangos(db).GetPageAsync(new RankReportFilter(RankReportMode.Lifetime), Pagina())).TotalCount.Should().Be(3);
    }
}
