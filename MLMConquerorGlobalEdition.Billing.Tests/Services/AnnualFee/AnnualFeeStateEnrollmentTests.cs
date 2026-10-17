using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MLMConquerorGlobalEdition.Billing.Services.AnnualFee;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.AnnualFee;

/// <summary>
/// Alta de los estados de cuota (Plan K, K3): uno por Ambassador no borrado y no Terminated, con el
/// primer aniversario que no haya pasado, por lotes e idempotente. Sin plan anual activo no crea nada.
/// </summary>
public class AnnualFeeStateEnrollmentTests
{
    private static AnnualFeeStateEnrollment Servicio(AppDbContext db) =>
        new(db, CuotaAnualDb.Reloj(), NullLogger<AnnualFeeStateEnrollment>.Instance);

    [Fact]
    public async Task Crea_un_estado_de_cuota_por_Ambassador_con_su_primer_vencimiento()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "AMB-1", alta: new DateTime(2026, 6, 20, 14, 30, 0));
        await CuotaAnualDb.MiembroAsync(db, "AMB-2", alta: new DateTime(2025, 6, 1));

        var creados = await Servicio(db).EnsureMissingAsync(1000, 10);

        creados.Should().Be(2);
        var estados = await db.SubscriptionBillingStates.AsNoTracking().OrderBy(s => s.MemberId).ToListAsync();
        estados.Should().OnlyContain(s => s.Purpose == BillingStatePurpose.AnnualFee
                                          && s.MembershipSubscriptionId == null
                                          && s.RecurringBillingPlanId == CuotaAnualDb.PlanAnual
                                          && s.Status == RecurringBillingStatus.Active
                                          && s.CurrentAttemptIndex == 0
                                          && s.NextAttemptDate == s.NextBillingDate);
        estados[0].NextBillingDate.Should().Be(new DateTime(2027, 6, 20));  // el aniversario de este año aún no pasó
        estados[0].BillingAnchorDate.Should().Be(new DateTime(2026, 6, 20));
        estados[1].NextBillingDate.Should().Be(new DateTime(2028, 6, 1));   // 2027-06-01 ya pasó: no hacia atrás
    }

    [Fact]
    public async Task No_crea_estados_para_clientes_ni_terminados_ni_borrados()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "MBR-1", tipo: MemberType.ExternalMember);
        await CuotaAnualDb.MiembroAsync(db, "AMB-T", estado: MemberAccountStatus.Terminated);
        await CuotaAnualDb.MiembroAsync(db, "AMB-B", borrado: true);
        await CuotaAnualDb.MiembroAsync(db, "AMB-P", estado: MemberAccountStatus.Pending);
        await CuotaAnualDb.MiembroAsync(db, "AMB-S", estado: MemberAccountStatus.Suspended);

        await Servicio(db).EnsureMissingAsync(1000, 10);

        (await db.SubscriptionBillingStates.Select(s => s.MemberId).OrderBy(x => x).ToListAsync())
            .Should().Equal("AMB-P", "AMB-S");
    }

    [Fact]
    public async Task Es_idempotente()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "AMB-1");

        (await Servicio(db).EnsureMissingAsync(1000, 10)).Should().Be(1);
        (await Servicio(db).EnsureMissingAsync(1000, 10)).Should().Be(0);
        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Recorre_todos_los_lotes_y_respeta_el_tope()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        for (var i = 1; i <= 5; i++) await CuotaAnualDb.MiembroAsync(db, $"AMB-{i}");

        (await Servicio(db).EnsureMissingAsync(batchSize: 2, maxBatches: 1)).Should().Be(2);
        (await Servicio(db).EnsureMissingAsync(batchSize: 2, maxBatches: 10)).Should().Be(3);
        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task Sin_plan_anual_activo_no_crea_nada()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db, activo: false);
        await CuotaAnualDb.MiembroAsync(db, "AMB-1");

        (await Servicio(db).EnsureMissingAsync(1000, 10)).Should().Be(0);
        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Para_un_miembro_lo_crea_si_falta_y_devuelve_el_que_hay_con_su_plan()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "AMB-1");

        var primero = await Servicio(db).EnsureForMemberAsync("AMB-1");
        var segundo = await Servicio(db).EnsureForMemberAsync("AMB-1");

        primero.Should().NotBeNull();
        segundo!.Id.Should().Be(primero!.Id);
        segundo.Plan!.IsAnnualFeePlan.Should().BeTrue();
        segundo.Plan.PlanProducts.Should().ContainSingle();
        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Para_un_cliente_no_crea_nada()
    {
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "MBR-1", tipo: MemberType.ExternalMember);

        (await Servicio(db).EnsureForMemberAsync("MBR-1")).Should().BeNull();
    }

    [Fact]
    public async Task Con_la_membresia_en_HoldByBilling_tambien_crea_el_estado()
    {
        // Nick (2026-10-17): mientras la membresía esté en HoldByBilling no se cobra la cuota, pero el
        // estado se crea igual para que esté listo cuando salga de la espera (K3, R5).
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "AMB-H");
        db.MembershipSubscriptions.Add(new MLMConquerorGlobalEdition.Domain.Entities.Membership.MembershipSubscription
        {
            MemberId = "AMB-H", MembershipLevelId = 3, StartDate = new DateTime(2026, 6, 5),
            SubscriptionStatus = MLMConquerorGlobalEdition.Domain.Entities.Membership.MembershipStatus.HoldByBilling,
            CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        (await Servicio(db).EnsureMissingAsync(1000, 10)).Should().Be(1);
    }

    [Fact]
    public async Task Un_estado_de_cuota_borrado_cuenta_como_ya_creado()
    {
        // El índice único filtrado incluye los borrados lógicos: volver a crearlo chocaría en SQL Server.
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "AMB-1");
        var e = await CuotaAnualDb.EstadoAsync(db, "AMB-1");
        e.IsDeleted = true;
        await db.SaveChangesAsync();

        (await Servicio(db).EnsureMissingAsync(1000, 10)).Should().Be(0);
    }

    // ── Choques y fallos al guardar (revisión I4 y M1) ─────────────────────────

    private static int EstadosNuevos(Microsoft.EntityFrameworkCore.DbContext c) =>
        c.ChangeTracker.Entries<MLMConquerorGlobalEdition.Domain.Entities.Billing.SubscriptionBillingState>()
         .Count(e => e.State == EntityState.Added);

    [Fact]
    public async Task Si_otra_peticion_lo_crea_a_la_vez_devuelve_el_suyo_sin_limpiar_el_contexto()
    {
        var nombre = Guid.NewGuid().ToString();
        var fallos = new MLMConquerorGlobalEdition.Billing.Tests.Helpers.SaveFaults();
        await using var db = CuotaAnualDb.Nueva(nombre, fallos);
        await CuotaAnualDb.PlanAsync(db);
        var perfil = await CuotaAnualDb.MiembroAsync(db, "AMB-1");
        fallos.FailWhen(c => EstadosNuevos(c) > 0, times: 1, make: _ =>
        {
            using var otra = CuotaAnualDb.Nueva(nombre);
            otra.SubscriptionBillingStates.Add(new MLMConquerorGlobalEdition.Domain.Entities.Billing.SubscriptionBillingState
            {
                Id = "el-de-la-otra", Purpose = BillingStatePurpose.AnnualFee, MemberId = "AMB-1",
                RecurringBillingPlanId = CuotaAnualDb.PlanAnual, BillingAnchorDate = new DateTime(2026, 6, 5),
                NextBillingDate = new DateTime(2028, 6, 5), NextAttemptDate = new DateTime(2028, 6, 5), CreatedBy = "otra"
            });
            otra.SaveChanges();
            return new DbUpdateException("Cannot insert duplicate key row (UX_SubscriptionBillingStates_AnnualFeeMember).");
        });

        var estado = await Servicio(db).EnsureForMemberAsync("AMB-1");

        estado!.Id.Should().Be("el-de-la-otra");
        db.Entry(perfil).State.Should().Be(EntityState.Unchanged);
        (await db.SubscriptionBillingStates.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Si_el_guardado_falla_y_no_hay_fila_relanza()
    {
        var fallos = new MLMConquerorGlobalEdition.Billing.Tests.Helpers.SaveFaults();
        await using var db = CuotaAnualDb.Nueva(interceptor: fallos);
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "AMB-1");
        fallos.FailWhen(c => EstadosNuevos(c) > 0, times: 1);

        var act = () => Servicio(db).EnsureForMemberAsync("AMB-1");

        await act.Should().ThrowAsync<DbUpdateException>();
        EstadosNuevos(db).Should().Be(0);
    }

    [Fact]
    public async Task Un_estado_borrado_no_se_resucita_para_un_miembro()
    {
        // Alguien lo borró a propósito: no se crea otro (el índice único lo impediría) ni se restaura en
        // silencio, que volvería a cobrar. Se informa en el registro y no hay estado.
        await using var db = CuotaAnualDb.Nueva();
        await CuotaAnualDb.PlanAsync(db);
        await CuotaAnualDb.MiembroAsync(db, "AMB-1");
        var e = await CuotaAnualDb.EstadoAsync(db, "AMB-1");
        e.IsDeleted = true;
        await db.SaveChangesAsync();

        (await Servicio(db).EnsureForMemberAsync("AMB-1")).Should().BeNull();
        (await db.SubscriptionBillingStates.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Si_falla_un_lote_reintenta_fila_a_fila_y_salta_solo_la_que_falla()
    {
        var fallos = new MLMConquerorGlobalEdition.Billing.Tests.Helpers.SaveFaults();
        await using var db = CuotaAnualDb.Nueva(interceptor: fallos);
        await CuotaAnualDb.PlanAsync(db);
        foreach (var m in new[] { "AMB-1", "AMB-2", "AMB-MAL", "AMB-3" }) await CuotaAnualDb.MiembroAsync(db, m);
        fallos.FailWhen(c => EstadosNuevos(c) > 1
            || c.ChangeTracker.Entries<MLMConquerorGlobalEdition.Domain.Entities.Billing.SubscriptionBillingState>()
                .Any(x => x.State == EntityState.Added && x.Entity.MemberId == "AMB-MAL"));

        var creados = await Servicio(db).EnsureMissingAsync(1000, 10);

        creados.Should().Be(3);
        (await db.SubscriptionBillingStates.Select(s => s.MemberId).OrderBy(x => x).ToListAsync())
            .Should().Equal("AMB-1", "AMB-2", "AMB-3");
        EstadosNuevos(db).Should().Be(0);
    }
}
