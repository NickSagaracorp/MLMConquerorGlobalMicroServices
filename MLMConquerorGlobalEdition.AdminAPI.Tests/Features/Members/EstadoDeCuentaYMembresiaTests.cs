using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.AdminAPI.DTOs.Members;
using MLMConquerorGlobalEdition.AdminAPI.Features.Members.UpdateMemberStatus;
using MLMConquerorGlobalEdition.AdminAPI.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.Repository.Services.Billing;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;

namespace MLMConquerorGlobalEdition.AdminAPI.Tests.Features.Members;

/// <summary>
/// Plan Q (Q3, decidido por Nick): suspender o inactivar la cuenta desde el admin retiene (OnHold) sus membresias activas
/// y para su cobro; terminarla las cancela; volver a Active reactiva solo las que retuvo el admin. Los puntos los mueve el
/// interceptor (SqlServerDevolucionYEstadoDeCuentaTests, en BizCenter.Tests).
/// </summary>
public class EstadoDeCuentaYMembresiaTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 30, 0);
    private const string Miembro = "AMB-000001";

    private static async Task<AppDbContext> SembrarAsync(MemberAccountStatus estadoCuenta = MemberAccountStatus.Active)
    {
        var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = Miembro, FirstName = "Bob", LastName = "Jones", Country = "US", Status = estadoCuenta,
            MemberType = MemberType.Ambassador, EnrollDate = Ahora.AddDays(-90), CreatedBy = "prueba"
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-activa", MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            StartDate = Ahora.AddDays(-40), IsAutoRenew = true, CreatedBy = "prueba"
        });
        // Retenida por otro motivo (soporte, cobro): sin la marca del admin.
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-retenida", MemberId = Miembro, MembershipLevelId = 1, SubscriptionStatus = MembershipStatus.OnHold,
            StartDate = Ahora.AddDays(-200), HoldDate = Ahora.AddDays(-100), CreatedBy = "prueba"
        });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado-1", MembershipSubscriptionId = "sub-activa", MemberId = Miembro, RecurringBillingPlanId = 1,
            BillingAnchorDate = Ahora.AddDays(-40), NextBillingDate = Ahora.AddDays(20), NextAttemptDate = Ahora.AddDays(20),
            Status = RecurringBillingStatus.Active, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private static async Task CambiarAsync(AppDbContext db, MemberAccountStatus estado, DateTime? cuando = null)
    {
        var usuario = new Mock<ICurrentUserService>();
        usuario.Setup(u => u.UserId).Returns("admin-001");
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(d => d.Now).Returns(cuando ?? Ahora);
        var r = await new UpdateMemberStatusHandler(db, usuario.Object, reloj.Object, new NoOpCacheService())
            .Handle(new UpdateMemberStatusCommand(Miembro, new UpdateMemberStatusRequest { Status = estado }), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        db.ChangeTracker.Clear();
    }

    private static Task<MembershipSubscription> SubAsync(AppDbContext db, string id) =>
        db.MembershipSubscriptions.AsNoTracking().SingleAsync(s => s.Id == id);

    [Theory]
    [InlineData(MemberAccountStatus.Suspended)]
    [InlineData(MemberAccountStatus.Inactive)]
    public async Task Suspender_o_inactivar_retiene_las_activas_con_la_marca_y_para_su_cobro(MemberAccountStatus estado)
    {
        await using var db = await SembrarAsync();

        await CambiarAsync(db, estado);

        var sub = await SubAsync(db, "sub-activa");
        sub.SubscriptionStatus.Should().Be(MembershipStatus.OnHold);
        sub.HeldByAccountStatus.Should().Be(estado);
        sub.HoldDate.Should().Be(Ahora);
        var cobro = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        cobro.Status.Should().Be(RecurringBillingStatus.Stopped);
        cobro.LastFailureReason.Should().Be(MembershipBillingStops.ReasonMembershipOnHold);
        (await SubAsync(db, "sub-retenida")).HeldByAccountStatus.Should().BeNull();
    }

    [Fact]
    public async Task Reactivar_devuelve_solo_las_que_retuvo_el_admin_y_reanuda_su_cobro()
    {
        await using var db = await SembrarAsync();
        await CambiarAsync(db, MemberAccountStatus.Suspended);

        await CambiarAsync(db, MemberAccountStatus.Active, Ahora.AddDays(3));

        var sub = await SubAsync(db, "sub-activa");
        sub.SubscriptionStatus.Should().Be(MembershipStatus.Active);
        sub.HeldByAccountStatus.Should().BeNull();
        sub.HoldDate.Should().BeNull();
        (await SubAsync(db, "sub-retenida")).SubscriptionStatus.Should().Be(MembershipStatus.OnHold,
            "una retenida por otro motivo (p. ej. el cobro) no la reactiva el admin");
        var cobro = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        cobro.Status.Should().Be(RecurringBillingStatus.Active);
        cobro.LastFailureReason.Should().BeNull();
        cobro.NextAttemptDate.Should().Be(Ahora.AddDays(20), "el ciclo no vencio durante la retencion: sigue igual");
    }

    [Fact]
    public async Task Si_el_ciclo_vencio_durante_la_retencion_se_cobra_al_reactivar_y_el_ciclo_arranca_ese_dia()
    {
        await using var db = await SembrarAsync();
        await CambiarAsync(db, MemberAccountStatus.Suspended);

        var vuelta = Ahora.AddDays(60);
        await CambiarAsync(db, MemberAccountStatus.Active, vuelta);

        var cobro = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        cobro.Status.Should().Be(RecurringBillingStatus.Active);
        cobro.NextBillingDate.Should().Be(vuelta.Date);
        cobro.NextAttemptDate.Should().Be(vuelta.Date);
        cobro.BillingAnchorDate.Should().Be(vuelta.Date);
        cobro.CurrentAttemptIndex.Should().Be(0);
    }

    [Fact]
    public async Task Un_cobro_parado_por_otro_motivo_no_se_reanuda_al_reactivar()
    {
        await using var db = await SembrarAsync();
        var cobro = await db.SubscriptionBillingStates.SingleAsync();
        MembershipBillingStops.Stop(cobro, "MANUAL", Ahora.AddDays(-1), "admin");
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await CambiarAsync(db, MemberAccountStatus.Suspended);
        await CambiarAsync(db, MemberAccountStatus.Active, Ahora.AddDays(1));

        var guardado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        guardado.Status.Should().Be(RecurringBillingStatus.Stopped);
        guardado.LastFailureReason.Should().Be("MANUAL");
    }

    [Fact]
    public async Task Terminar_cancela_las_activas_y_las_retenidas_por_el_admin_pero_no_las_de_otro_origen()
    {
        await using var db = await SembrarAsync();
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-otra-activa", MemberId = Miembro, MembershipLevelId = 2, SubscriptionStatus = MembershipStatus.Active,
            StartDate = Ahora.AddDays(-10), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        await CambiarAsync(db, MemberAccountStatus.Suspended);

        await CambiarAsync(db, MemberAccountStatus.Terminated, Ahora.AddDays(1));

        foreach (var id in new[] { "sub-activa", "sub-otra-activa" })
        {
            var sub = await SubAsync(db, id);
            sub.SubscriptionStatus.Should().Be(MembershipStatus.Cancelled, id);
            sub.CancellationDate.Should().Be(Ahora.AddDays(1));
            sub.HeldByAccountStatus.Should().BeNull();
        }
        (await SubAsync(db, "sub-retenida")).SubscriptionStatus.Should().Be(MembershipStatus.OnHold);
    }

    [Fact]
    public async Task Terminar_una_cuenta_activa_cancela_sus_activas_y_para_su_cobro()
    {
        await using var db = await SembrarAsync();

        await CambiarAsync(db, MemberAccountStatus.Terminated);

        (await SubAsync(db, "sub-activa")).SubscriptionStatus.Should().Be(MembershipStatus.Cancelled);
        var cobro = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        cobro.Status.Should().Be(RecurringBillingStatus.Stopped);
        cobro.LastFailureReason.Should().Be(MembershipBillingStops.ReasonMembershipCancelled);
    }

    [Fact]
    public async Task Volver_a_Active_desde_Terminated_no_reactiva_las_canceladas()
    {
        await using var db = await SembrarAsync();
        await CambiarAsync(db, MemberAccountStatus.Suspended);
        await CambiarAsync(db, MemberAccountStatus.Terminated, Ahora.AddDays(1));

        await CambiarAsync(db, MemberAccountStatus.Active, Ahora.AddDays(2));

        (await SubAsync(db, "sub-activa")).SubscriptionStatus.Should().Be(MembershipStatus.Cancelled);
    }

    /// <summary>
    /// Revision de Q3 (I3): terminar tambien cancela la retenida por el cobro (HoldByBilling) y la caducada sin reemplazo,
    /// y para su cobro: el motor no puede cobrarlas y reactivarlas en una cuenta terminada. La caducada reemplazada por una
    /// subida o bajada y la OnHold de otro origen no cambian.
    /// </summary>
    [Fact]
    public async Task Terminar_cancela_tambien_la_retenida_por_el_cobro_y_la_caducada_sin_reemplazo_y_para_su_cobro()
    {
        await using var db = await SembrarAsync();
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-cobro", MemberId = Miembro, MembershipLevelId = 2, SubscriptionStatus = MembershipStatus.HoldByBilling,
            StartDate = Ahora.AddDays(-70), IsAutoRenew = true, CreatedBy = "prueba"
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-caducada", MemberId = Miembro, MembershipLevelId = 4, SubscriptionStatus = MembershipStatus.Expired,
            StartDate = Ahora.AddDays(-120), IsAutoRenew = true, CreatedBy = "prueba"
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-reemplazada", MemberId = Miembro, MembershipLevelId = 5, SubscriptionStatus = MembershipStatus.Expired,
            StartDate = Ahora.AddDays(-150), CreatedBy = "prueba"
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-de-la-subida", MemberId = Miembro, MembershipLevelId = 6, PreviousMembershipLevelId = 5,
            SubscriptionStatus = MembershipStatus.Cancelled, StartDate = Ahora.AddDays(-140), CreatedBy = "prueba"
        });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado-cobro", MembershipSubscriptionId = "sub-cobro", MemberId = Miembro, RecurringBillingPlanId = 1,
            BillingAnchorDate = Ahora.AddDays(-70), NextBillingDate = Ahora.AddDays(-10), NextAttemptDate = Ahora.AddDays(1),
            Status = RecurringBillingStatus.Retrying, CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await CambiarAsync(db, MemberAccountStatus.Terminated);

        foreach (var id in new[] { "sub-activa", "sub-cobro", "sub-caducada" })
        {
            var sub = await SubAsync(db, id);
            sub.SubscriptionStatus.Should().Be(MembershipStatus.Cancelled, id);
            sub.CancellationDate.Should().Be(Ahora, id);
            sub.IsAutoRenew.Should().BeFalse(id);
        }
        (await SubAsync(db, "sub-reemplazada")).SubscriptionStatus.Should().Be(MembershipStatus.Expired);
        (await SubAsync(db, "sub-retenida")).SubscriptionStatus.Should().Be(MembershipStatus.OnHold);
        var cobro = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync(e => e.Id == "estado-cobro");
        cobro.Status.Should().Be(RecurringBillingStatus.Stopped);
        cobro.LastFailureReason.Should().Be(MembershipBillingStops.ReasonMembershipCancelled);
    }
}
