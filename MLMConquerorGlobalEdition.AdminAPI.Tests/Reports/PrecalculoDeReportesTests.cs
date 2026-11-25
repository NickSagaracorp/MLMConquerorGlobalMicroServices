using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MLMConquerorGlobalEdition.AdminAPI.Reports;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Grid;
using MLMConquerorGlobalEdition.Repository.Services.Reports;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using Moq;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Reports;

/// <summary>
/// El precálculo recalcula aunque haya copia guardada (así la pantalla nunca lee una lista vieja de más de una vuelta) y
/// solo trabaja mientras alguien usa los reportes.
/// </summary>
[Collection(CacheDeReportesCollection.Nombre)]
public class PrecalculoDeReportesTests
{
    private static readonly DateTime Corte = new(2026, 11, 13);

    private static AppDbContext Base()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.MemberProfiles.Add(new MemberProfile
        {
            Id = Guid.NewGuid().ToString(), MemberId = "AMB-1", FirstName = "Ana", LastName = "Lopez",
            Email = "ana@example.com", Country = "Bulgaria", CreatedBy = "seed", CreationDate = Corte, LastUpdateDate = Corte
        });
        Comision(db, 100m);
        db.SaveChanges();
        return db;
    }

    private static void Comision(AppDbContext db, decimal monto) =>
        db.CommissionEarnings.Add(new CommissionEarning
        {
            Id = Guid.NewGuid().ToString(), BeneficiaryMemberId = "AMB-1", Amount = monto, EarnedDate = Corte,
            PaymentDate = Corte, Status = CommissionEarningStatus.Paid, CommissionTypeId = 1,
            CreatedBy = "seed", CreationDate = Corte, LastUpdateDate = Corte
        });

    private static EarningsRankingService Servicio(AppDbContext db, IMemoryCache cache)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.SetupGet(r => r.Now).Returns(Corte.AddDays(1));
        return new EarningsRankingService(db, cache, reloj.Object);
    }

    [Fact]
    public async Task Precalcular_rehace_la_lista_aunque_este_guardada()
    {
        using var db = Base();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var servicio = Servicio(db, cache);

        (await servicio.GetAllAsync(Corte, null, null)).Single().Total.Should().Be(100m);

        Comision(db, 50m);
        await db.SaveChangesAsync();
        (await servicio.GetAllAsync(Corte, null, null)).Single().Total.Should().Be(100m, "la pantalla lee la copia guardada");

        await servicio.PrecalcularAsync(Corte, null);
        (await servicio.GetAllAsync(Corte, null, null)).Single().Total.Should().Be(150m);
    }

    [Fact]
    public async Task Precalcular_no_cuenta_como_uso_y_pedir_una_pagina_si()
    {
        using var db = Base();
        var servicio = Servicio(db, new MemoryCache(new MemoryCacheOptions()));

        await servicio.GetPageAsync(Corte, 180, new GridDataRequest { Page = 1, PageSize = 50 });
        var trasUsar = EarningsRankingService.TiempoSinUso;
        trasUsar.Should().NotBeNull();
        trasUsar!.Value.Should().BeLessThan(TimeSpan.FromMinutes(1));
    }

    [Theory]
    [InlineData(null, true, true)]     // al arrancar siempre
    [InlineData(null, false, false)]   // nadie lo abrió nunca
    [InlineData(30, false, true)]      // usado hace media hora
    [InlineData(180, false, false)]    // sin uso hace tres horas: la base descansa
    public void Recalcula_solo_mientras_se_usa(int? minutosSinUso, bool primeraVuelta, bool esperado) =>
        PrecalculoDeReportes.EnUso(minutosSinUso is null ? null : TimeSpan.FromMinutes(minutosSinUso.Value), primeraVuelta)
            .Should().Be(esperado);
}
