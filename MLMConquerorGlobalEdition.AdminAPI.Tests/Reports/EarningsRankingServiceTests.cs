using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

public class EarningsRankingServiceTests
{
    private static readonly DateTime Corte = new(2026, 11, 13);

    private static AppDbContext Base()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Miembro(db, "AMB-1", "Ana", "Lopez", "ana@example.com", "Bulgaria", "ana");
        Miembro(db, "AMB-2", "Beto", "Diaz", "beto@example.com", "France", "beto");
        Miembro(db, "AMB-3", "MWR", "Corp.", "info@example.com", "United States", "corp", corporativa: true);
        Miembro(db, "AMB-4", "Carla", "Ruiz", "carla@example.com", "Japan", null);
        return db;
    }

    private static void Miembro(AppDbContext db, string id, string nombre, string apellido, string email, string pais,
        string? slug, bool corporativa = false) =>
        db.MemberProfiles.Add(new MemberProfile
        {
            Id = Guid.NewGuid().ToString(), MemberId = id, FirstName = nombre, LastName = apellido, Email = email,
            Country = pais, ReplicateSiteSlug = slug, IsCorporateAccount = corporativa,
            CreatedBy = "seed", CreationDate = Corte, LastUpdateDate = Corte
        });

    private static void Comision(AppDbContext db, string beneficiario, decimal monto, DateTime fecha,
        CommissionEarningStatus estado = CommissionEarningStatus.Paid) =>
        db.CommissionEarnings.Add(new CommissionEarning
        {
            Id = Guid.NewGuid().ToString(), BeneficiaryMemberId = beneficiario, Amount = monto, EarnedDate = fecha,
            PaymentDate = fecha, Status = estado, CommissionTypeId = 1,
            CreatedBy = "seed", CreationDate = fecha, LastUpdateDate = fecha
        });

    private static EarningsRankingService Servicio(AppDbContext db) =>
        new(db, new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task Suma_pagadas_y_pendientes_sin_canceladas_ni_cuentas_corporativas()
    {
        using var db = Base();
        Comision(db, "AMB-1", 100, Corte.AddDays(-1));
        Comision(db, "AMB-1", 50, Corte.AddDays(-2), CommissionEarningStatus.Pending);
        Comision(db, "AMB-1", 999, Corte.AddDays(-3), CommissionEarningStatus.Cancelled);
        Comision(db, "AMB-2", 300, Corte.AddDays(-5));
        Comision(db, "AMB-3", 5000, Corte.AddDays(-5));
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetPageAsync(Corte, null, new GridDataRequest { Page = 1, PageSize = 20 });

        r.TotalCount.Should().Be(2);
        r.Items.Select(i => (i.Rank, i.MemberId, i.Total)).Should().Equal((1, "AMB-2", 300m), (2, "AMB-1", 150m));
    }

    [Fact]
    public async Task El_corte_incluye_todo_su_dia_y_excluye_lo_posterior()
    {
        using var db = Base();
        Comision(db, "AMB-1", 10, Corte.AddHours(23));
        Comision(db, "AMB-1", 70, Corte.AddDays(1));
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetPageAsync(Corte, null, new GridDataRequest());

        r.Items.Single().Total.Should().Be(10);
    }

    [Fact]
    public async Task La_ventana_cuenta_los_ultimos_N_dias_hasta_el_corte()
    {
        using var db = Base();
        Comision(db, "AMB-1", 10, Corte.AddDays(-179));
        Comision(db, "AMB-1", 70, Corte.AddDays(-180));
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetPageAsync(Corte, 180, new GridDataRequest());

        r.Items.Single().Total.Should().Be(10);
    }

    [Fact]
    public async Task Una_ventana_no_permitida_se_trata_como_180()
    {
        using var db = Base();
        Comision(db, "AMB-1", 10, Corte.AddDays(-100));
        Comision(db, "AMB-1", 70, Corte.AddDays(-200));
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetPageAsync(Corte, 7, new GridDataRequest());

        r.Items.Single().Total.Should().Be(10);
    }

    [Fact]
    public async Task Buscar_filtra_por_nombre_o_email_y_conserva_el_rank_global()
    {
        using var db = Base();
        Comision(db, "AMB-1", 100, Corte);
        Comision(db, "AMB-2", 300, Corte);
        Comision(db, "AMB-4", 200, Corte);
        await db.SaveChangesAsync();

        var porNombre = await Servicio(db).GetPageAsync(Corte, null, new GridDataRequest { Search = "carla ru" });
        var porEmail  = await Servicio(db).GetPageAsync(Corte, null, new GridDataRequest { Search = "ANA@EXAMPLE" });

        porNombre.Items.Single().Should().BeEquivalentTo(new { Rank = 2, MemberId = "AMB-4", Username = (string?)null,
            MemberName = "Carla Ruiz", Email = "carla@example.com", Country = "Japan", Total = 200m });
        porEmail.Items.Single().Should().BeEquivalentTo(new { Rank = 3, MemberId = "AMB-1", Username = "ana" });
    }

    [Fact]
    public async Task Empate_de_total_se_ordena_por_MemberId()
    {
        using var db = Base();
        Comision(db, "AMB-2", 100, Corte);
        Comision(db, "AMB-1", 100, Corte);
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetPageAsync(Corte, null, new GridDataRequest());

        r.Items.Select(i => i.MemberId).Should().Equal("AMB-1", "AMB-2");
    }

    [Fact]
    public async Task Pagina_con_tamano_acotado()
    {
        using var db = Base();
        Comision(db, "AMB-1", 100, Corte);
        Comision(db, "AMB-2", 300, Corte);
        Comision(db, "AMB-4", 200, Corte);
        await db.SaveChangesAsync();

        var r = await Servicio(db).GetPageAsync(Corte, null, new GridDataRequest { Page = 2, PageSize = 2 });

        r.TotalCount.Should().Be(3);
        r.Items.Select(i => i.Rank).Should().Equal(3);
    }

    [Fact]
    public async Task Exportar_devuelve_todas_las_filas_filtradas()
    {
        using var db = Base();
        Comision(db, "AMB-1", 100, Corte);
        Comision(db, "AMB-2", 300, Corte);
        await db.SaveChangesAsync();

        var filas = await Servicio(db).GetAllAsync(Corte, null, search: null);

        filas.Select(f => f.MemberId).Should().Equal("AMB-2", "AMB-1");
    }
}
