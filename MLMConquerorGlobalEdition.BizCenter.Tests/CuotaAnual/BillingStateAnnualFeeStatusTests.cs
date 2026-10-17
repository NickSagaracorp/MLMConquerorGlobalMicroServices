using MLMConquerorGlobalEdition.BizCenter.Tests.Reportes;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.AnnualFee;
using MLMConquerorGlobalEdition.Repository.Services.Teams;

namespace MLMConquerorGlobalEdition.BizCenter.Tests.CuotaAnual;

/// <summary>
/// Plan K (K16): «anualidad pagada» = el estado de cuota anual está al día (vence después de hoy). Es la
/// rama (b) de «Calificado» del Plan J, que hasta ahora nunca se cumplía (AnnualFeeNotModeled).
/// </summary>
public class BillingStateAnnualFeeStatusTests
{
    private static readonly DateTime Hoy = CuotaAnualSiembra.Ahora.Date;

    private static async Task<IReadOnlySet<string>> PagadosAsync(Action<AppDbContext> siembra, params string[] ids)
    {
        await using var db = ReportesDb.Nueva();
        siembra(db);
        await db.SaveChangesAsync();
        return await new BillingStateAnnualFeeStatus(db, CuotaAnualSiembra.Reloj()).PaidCurrentAnnualFeeAsync(ids);
    }

    [Fact]
    public async Task Un_estado_que_vence_despues_de_hoy_esta_pagado()
        => (await PagadosAsync(db => CuotaAnualSiembra.Estado(db, "AMB-1", Hoy.AddDays(1)), "AMB-1"))
            .Should().BeEquivalentTo(new[] { "AMB-1" });

    [Fact]
    public async Task Si_vence_hoy_ya_no_esta_pagado()
        => (await PagadosAsync(db => CuotaAnualSiembra.Estado(db, "AMB-1", Hoy), "AMB-1")).Should().BeEmpty();

    [Fact]
    public async Task Un_estado_vencido_no_esta_pagado()
        => (await PagadosAsync(db => CuotaAnualSiembra.Estado(db, "AMB-1", Hoy.AddDays(-20)), "AMB-1")).Should().BeEmpty();

    [Fact]
    public async Task Un_estado_parado_no_cuenta()
        => (await PagadosAsync(db => CuotaAnualSiembra.Estado(db, "AMB-1", Hoy.AddDays(100), RecurringBillingStatus.Stopped), "AMB-1"))
            .Should().BeEmpty();

    [Fact]
    public async Task Un_estado_de_membresia_no_es_la_cuota()
        => (await PagadosAsync(db => CuotaAnualSiembra.Estado(db, "AMB-1", Hoy.AddDays(30), proposito: BillingStatePurpose.Membership), "AMB-1"))
            .Should().BeEmpty();

    [Fact]
    public async Task Sin_estado_no_esta_pagado_y_por_lote_solo_salen_los_pagados()
        => (await PagadosAsync(db =>
            {
                CuotaAnualSiembra.Estado(db, "AMB-1", Hoy.AddDays(200));
                CuotaAnualSiembra.Estado(db, "AMB-2", Hoy.AddDays(-1));
            }, "AMB-1", "AMB-2", "AMB-3", "AMB-1"))
            .Should().BeEquivalentTo(new[] { "AMB-1" });

    [Fact]
    public async Task Calificado_con_la_anualidad_real_inscrito_hace_dos_anios()
    {
        await using var db = ReportesDb.Nueva();
        var alta = CuotaAnualSiembra.Ahora.AddYears(-2);
        ReportesDb.Miembro(db, "AMB-1", alta: alta);
        ReportesDb.Miembro(db, "AMB-2", alta: alta);
        ReportesDb.Suscripcion(db, "AMB-1", 3, MembershipStatus.Active, alta);
        ReportesDb.Suscripcion(db, "AMB-2", 3, MembershipStatus.Active, alta);
        CuotaAnualSiembra.Estado(db, "AMB-1", Hoy.AddDays(120));
        CuotaAnualSiembra.Estado(db, "AMB-2", Hoy.AddDays(-3));
        await db.SaveChangesAsync();

        var reloj = CuotaAnualSiembra.Reloj();
        var calificado = await new QualificationService(db, reloj, new BillingStateAnnualFeeStatus(db, reloj))
            .AreQualifiedAsync(new[] { "AMB-1", "AMB-2" });

        calificado["AMB-1"].Should().BeTrue();
        calificado["AMB-2"].Should().BeFalse();
    }
}
