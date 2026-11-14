using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLMConquerorGlobalEdition.AdminAPI.Controllers;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class RankReportServiceTests
{
    private static readonly DateTime Hoy = new(2026, 11, 14);

    private static void Miembro(AppDbContext db, string id, string nombre, string apellido, string pais,
        MemberAccountStatus estado = MemberAccountStatus.Active, MemberType tipo = MemberType.Ambassador,
        bool corporativa = false) =>
        db.MemberProfiles.Add(new MemberProfile
        {
            Id = Guid.NewGuid().ToString(), MemberId = id, FirstName = nombre, LastName = apellido,
            Email = $"{nombre.ToLowerInvariant()}@example.com", Country = pais, Status = estado, MemberType = tipo,
            IsCorporateAccount = corporativa, CreatedBy = "seed", CreationDate = Hoy, LastUpdateDate = Hoy
        });

    private static void Rango(AppDbContext db, string id, int rango, DateTime fecha) =>
        RangoDesdeQueriesTests.Rango(db, id, rango, fecha);

    /// <summary>
    /// AMB-1 Ana: sube y baja (de por vida Gold desde el 1-mar, vigente Silver desde el 1-jul). AMB-2 Beto (Suspended):
    /// Platinum. AMB-7 Fito (Inactive): de por vida Silver, vigente el rango 0. Fuera siempre: AMB-3 (solo rango 0),
    /// AMB-4 (Terminated), AMB-5 (corporativa), CUS-1 (cliente), AMB-6 (sin historial).
    /// </summary>
    private static AppDbContext Base()
    {
        var db = RangoDesdeQueriesTests.ConRangos();
        Miembro(db, "AMB-1", "Ana", "Lopez", "Bulgaria");
        Rango(db, "AMB-1", 1, new DateTime(2026, 1, 10));
        Rango(db, "AMB-1", 2, new DateTime(2026, 3, 1, 9, 0, 0));
        Rango(db, "AMB-1", 1, new DateTime(2026, 5, 1));
        Rango(db, "AMB-1", 2, new DateTime(2026, 6, 1));
        Rango(db, "AMB-1", 1, new DateTime(2026, 7, 1));
        Miembro(db, "AMB-2", "Beto", "Diaz", "France", MemberAccountStatus.Suspended);
        Rango(db, "AMB-2", 3, new DateTime(2026, 4, 1));
        Miembro(db, "AMB-3", "Carla", "Ruiz", "Japan");
        Rango(db, "AMB-3", 20, new DateTime(2026, 2, 1));
        Miembro(db, "AMB-4", "Dora", "Paz", "Chile", MemberAccountStatus.Terminated);
        Rango(db, "AMB-4", 2, new DateTime(2026, 2, 1));
        Miembro(db, "AMB-5", "MWR", "Corp.", "United States", corporativa: true);
        Rango(db, "AMB-5", 3, new DateTime(2026, 2, 1));
        Miembro(db, "CUS-1", "Eli", "Cruz", "Spain", tipo: MemberType.ExternalMember);
        Rango(db, "CUS-1", 1, new DateTime(2026, 2, 1));
        Miembro(db, "AMB-6", "Eva", "Mora", "Mexico", MemberAccountStatus.Pending);
        Miembro(db, "AMB-7", "Fito", "Gil", "Peru", MemberAccountStatus.Inactive);
        Rango(db, "AMB-7", 1, new DateTime(2026, 2, 1));
        Rango(db, "AMB-7", 20, new DateTime(2026, 8, 1));
        db.SaveChanges();
        return db;
    }

    private static RankReportService Servicio(AppDbContext db, IMemoryCache? cache = null) =>
        new(db, cache ?? new MemoryCache(new MemoryCacheOptions()));

    private static async Task<List<string>> Ids(AppDbContext db, RankReportFilter filtro, IMemoryCache? cache = null) =>
        (await Servicio(db, cache).GetAllAsync(filtro)).Select(f => f.MemberId).ToList();

    [Fact]
    public async Task Lifetime_da_el_mayor_rango_desde_la_primera_vez_y_excluye_a_quien_no_debe_salir()
    {
        using var db = Base();

        var filas = await Servicio(db).GetAllAsync(new RankReportFilter(RankReportMode.Lifetime));

        filas.Select(f => (f.MemberId, f.RankName, f.Since)).Should().Equal(
            ("AMB-2", "Platinum", new DateTime(2026, 4, 1)),
            ("AMB-1", "Gold", new DateTime(2026, 3, 1, 9, 0, 0)),
            ("AMB-7", "Silver", new DateTime(2026, 2, 1)));
    }

    [Fact]
    public async Task Current_da_la_fila_mas_reciente_y_saca_al_que_hoy_tiene_el_rango_0()
    {
        using var db = Base();

        var filas = await Servicio(db).GetAllAsync(new RankReportFilter(RankReportMode.Current));

        filas.Select(f => (f.MemberId, f.RankName, f.Since)).Should().Equal(
            ("AMB-2", "Platinum", new DateTime(2026, 4, 1)),
            ("AMB-1", "Silver", new DateTime(2026, 7, 1)));
    }

    [Fact]
    public async Task A_igual_rango_va_primero_la_fecha_mas_reciente()
    {
        using var db = Base();
        Miembro(db, "AMB-8", "Gabi", "Sol", "Peru");
        Rango(db, "AMB-8", 1, new DateTime(2026, 9, 1));
        await db.SaveChangesAsync();

        (await Ids(db, new RankReportFilter(RankReportMode.Current))).Should().Equal("AMB-2", "AMB-8", "AMB-1");
    }

    [Fact]
    public async Task La_fila_trae_nombre_email_pais_estado_y_rango()
    {
        using var db = Base();

        var ana = (await Servicio(db).GetAllAsync(new RankReportFilter(RankReportMode.Lifetime)))
            .Single(f => f.MemberId == "AMB-1");

        ana.Should().BeEquivalentTo(new
        {
            MemberId = "AMB-1", MemberName = "Ana Lopez", Email = "ana@example.com", Country = "Bulgaria",
            Status = MemberAccountStatus.Active, RankDefinitionId = 2, RankName = "Gold", RankSortOrder = 2
        });
    }

    [Fact]
    public async Task Filtra_por_rango()
        => (await Ids(Base(), new RankReportFilter(RankReportMode.Current, RankDefinitionId: 1))).Should().Equal("AMB-1");

    [Fact]
    public async Task Filtra_por_estados()
        => (await Ids(Base(), new RankReportFilter(RankReportMode.Lifetime, Statuses: new[] { MemberAccountStatus.Suspended })))
            .Should().Equal("AMB-2");

    [Fact]
    public async Task Terminated_nunca_aparece_aunque_se_pida()
        => (await Ids(Base(), new RankReportFilter(RankReportMode.Lifetime,
                Statuses: new[] { MemberAccountStatus.Terminated, MemberAccountStatus.Active })))
            .Should().Equal("AMB-1");

    [Fact]
    public async Task El_rango_de_fechas_de_Since_es_por_dia_completo_e_inclusivo()
        => (await Ids(Base(), new RankReportFilter(RankReportMode.Lifetime,
                SinceFrom: new DateTime(2026, 3, 1), SinceTo: new DateTime(2026, 3, 1))))
            .Should().Equal("AMB-1");

    [Theory]
    [InlineData("FRANCE", "AMB-2")]
    [InlineData("ana@", "AMB-1")]
    [InlineData("fito gi", "AMB-7")]
    public async Task Busca_por_nombre_email_o_pais(string termino, string esperado)
        => (await Ids(Base(), new RankReportFilter(RankReportMode.Lifetime, Search: termino))).Should().Equal(esperado);

    [Fact]
    public async Task Pagina_y_cuenta_con_la_busqueda_de_la_rejilla()
    {
        using var db = Base();
        var filtro = new RankReportFilter(RankReportMode.Lifetime);

        var pagina2 = await Servicio(db).GetPageAsync(filtro, new GridDataRequest { Page = 2, PageSize = 2 });
        var buscado = await Servicio(db).GetPageAsync(filtro, new GridDataRequest { Search = "beto" });

        pagina2.TotalCount.Should().Be(3);
        pagina2.Items.Select(i => i.MemberId).Should().Equal("AMB-7");
        buscado.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task InvalidateAll_obliga_a_recalcular()
    {
        using var db = Base();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var filtro = new RankReportFilter(RankReportMode.Lifetime);

        var antes = await Ids(db, filtro, cache);
        Rango(db, "AMB-6", 2, new DateTime(2026, 10, 1));
        await db.SaveChangesAsync();
        var sinInvalidar = await Ids(db, filtro, cache);
        RankReportService.InvalidateAll();
        var despues = await Ids(db, filtro, cache);

        antes.Should().HaveCount(3);
        sinInvalidar.Should().HaveCount(3);
        despues.Should().HaveCount(4).And.Contain("AMB-6");
    }
}
