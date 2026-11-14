using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Moq;

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

    private static IDateTimeProvider Reloj(DateTime ahora)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(ahora);
        reloj.SetupGet(r => r.UtcNow).Returns(ahora.ToUniversalTime());
        return reloj.Object;
    }

    private static EarningsRankingService Servicio(AppDbContext db, IMemoryCache? cache = null, DateTime? ahora = null) =>
        new(db, cache ?? new MemoryCache(new MemoryCacheOptions()), Reloj(ahora ?? Corte.AddDays(1)));

    /// <summary>Cache que cuenta las escrituras y, en las dos primeras lecturas, espera a que ambos pedidos hayan llegado.</summary>
    private sealed class CacheContada : IMemoryCache
    {
        private readonly MemoryCache _inner = new(new MemoryCacheOptions());
        private readonly CountdownEvent _llegada = new(2);
        private int _lecturas;
        public int Escrituras;
        public int Entradas => _inner.Count;

        public bool TryGetValue(object key, out object? value)
        {
            if (Interlocked.Increment(ref _lecturas) <= 2)
            {
                _llegada.Signal();
                _llegada.Wait(TimeSpan.FromSeconds(5));
            }
            return _inner.TryGetValue(key, out value);
        }

        public ICacheEntry CreateEntry(object key)
        {
            Interlocked.Increment(ref Escrituras);
            return _inner.CreateEntry(key);
        }

        public void Remove(object key) => _inner.Remove(key);
        public void Dispose() => _inner.Dispose();
    }

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

    [Fact]
    public async Task Quien_neto_es_cero_o_negativo_no_aparece()
    {
        using var db = Base();
        Comision(db, "AMB-1", 50, Corte);
        Comision(db, "AMB-1", -50, Corte);
        Comision(db, "AMB-2", -10, Corte);
        Comision(db, "AMB-4", 5, Corte);
        await db.SaveChangesAsync();

        var filas = await Servicio(db).GetAllAsync(Corte, null, null);

        filas.Select(f => f.MemberId).Should().Equal("AMB-4");
    }

    [Fact]
    public async Task Borde_inferior_de_la_ventana_con_hora_del_dia()
    {
        using var db = Base();
        Comision(db, "AMB-1", 10, Corte.AddDays(-180).AddHours(23));  // aun dentro del dia -180: fuera
        Comision(db, "AMB-2", 20, Corte.AddDays(-179));               // primer instante incluido
        await db.SaveChangesAsync();

        var filas = await Servicio(db).GetAllAsync(Corte, 180, null);

        filas.Select(f => f.MemberId).Should().Equal("AMB-2");
    }

    [Fact]
    public async Task La_cache_separa_ventanas_y_cortes()
    {
        using var db = Base();
        Comision(db, "AMB-1", 100, Corte.AddDays(-300));
        Comision(db, "AMB-1", 7, Corte.AddDays(-10));
        Comision(db, "AMB-1", 1000, Corte.AddDays(2));
        await db.SaveChangesAsync();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var ahora = Corte.AddDays(5);

        var vida     = await Servicio(db, cache, ahora).GetAllAsync(Corte, null, null);
        var ventana  = await Servicio(db, cache, ahora).GetAllAsync(Corte, 180, null);
        var corteTarde = await Servicio(db, cache, ahora).GetAllAsync(Corte.AddDays(2), null, null);

        vida.Single().Total.Should().Be(107);
        ventana.Single().Total.Should().Be(7);
        corteTarde.Single().Total.Should().Be(1107);
    }

    [Fact]
    public async Task El_corte_futuro_se_acota_a_hoy()
    {
        using var db = Base();
        Comision(db, "AMB-1", 10, Corte);
        Comision(db, "AMB-1", 90, Corte.AddDays(3));
        await db.SaveChangesAsync();

        var filas = await Servicio(db, ahora: Corte.AddHours(9)).GetAllAsync(Corte.AddDays(10), null, null);

        filas.Single().Total.Should().Be(10);
    }

    [Fact]
    public async Task Pedidos_simultaneos_en_frio_calculan_una_sola_vez()
    {
        var nombre = Guid.NewGuid().ToString();
        AppDbContext Abrir() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(nombre).Options);
        using (var semilla = Abrir())
        {
            Miembro(semilla, "AMB-1", "Ana", "Lopez", "ana@example.com", "Bulgaria", "ana");
            Comision(semilla, "AMB-1", 100, Corte);
            await semilla.SaveChangesAsync();
        }
        using var cache = new CacheContada();
        using var db1 = Abrir();
        using var db2 = Abrir();

        var t1 = Task.Run(() => Servicio(db1, cache).GetAllAsync(Corte, null, null));
        var t2 = Task.Run(() => Servicio(db2, cache).GetAllAsync(Corte, null, null));
        var r = await Task.WhenAll(t1, t2);

        cache.Escrituras.Should().Be(1);
        cache.Entradas.Should().Be(1);
        r[0].Should().BeEquivalentTo(r[1]);
        r[0].Single().Total.Should().Be(100);
    }

    [Fact]
    public async Task La_lista_devuelta_no_es_la_cacheada()
    {
        using var db = Base();
        Comision(db, "AMB-1", 100, Corte);
        await db.SaveChangesAsync();
        var servicio = Servicio(db);

        var primera = await servicio.GetAllAsync(Corte, null, null);
        if (primera is IList<EarningsRankRowView> lista && !lista.IsReadOnly)
            lista.Clear();
        var segunda = await servicio.GetAllAsync(Corte, null, null);

        segunda.Should().ContainSingle();
        ReferenceEquals(primera, segunda).Should().BeFalse();
    }
}
