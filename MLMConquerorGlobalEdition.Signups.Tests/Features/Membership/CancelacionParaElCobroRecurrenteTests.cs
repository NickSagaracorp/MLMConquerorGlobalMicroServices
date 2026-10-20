using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Member;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel.Interfaces;
using MLMConquerorGlobalEdition.SignupAPI.Features.Membership.Commands.CancelMembership;
using MLMConquerorGlobalEdition.SignupAPI.Jobs;
using MLMConquerorGlobalEdition.SignupAPI.Services;
using MLMConquerorGlobalEdition.SignupAPI.Tests.Helpers;

namespace MLMConquerorGlobalEdition.SignupAPI.Tests.Features.Membership;

/// <summary>
/// Plan K (revisión, C2): al cancelar una membresía se para también su estado de cobro recurrente, para
/// que el motor de renovación no vuelva a cobrarla. La cancelación inmediata lo para al momento; la
/// programada, cuando ProcessScheduledCancellationsJob la cierra.
/// </summary>
public class CancelacionParaElCobroRecurrenteTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 12, 0, 0);
    private const string Miembro = "AMB-000077";

    private static IDateTimeProvider Reloj()
    {
        var m = new Mock<IDateTimeProvider>();
        m.Setup(d => d.Now).Returns(Ahora);
        return m.Object;
    }

    private static async Task<(AppDbContext Db, string SubId)> SembrarAsync(DateTime? cancelacion = null)
    {
        var db = InMemoryDbHelper.Create();
        db.MemberProfiles.Add(new MemberProfile
        {
            MemberId = Miembro, FirstName = "T", LastName = "U", MemberType = MemberType.Ambassador,
            Status = MemberAccountStatus.Active, EnrollDate = Ahora.AddDays(-60), Country = "US", CreatedBy = "seed"
        });
        var sub = new MembershipSubscription
        {
            MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            ChangeReason = SubscriptionChangeReason.New, StartDate = Ahora.AddDays(-30), IsAutoRenew = cancelacion is null,
            CancellationDate = cancelacion, CreatedBy = "seed"
        };
        db.MembershipSubscriptions.Add(sub);
        await db.SaveChangesAsync();
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado-1", MembershipSubscriptionId = sub.Id, MemberId = Miembro, RecurringBillingPlanId = 1,
            BillingAnchorDate = Ahora.AddDays(-30), NextBillingDate = Ahora.Date, NextAttemptDate = Ahora.Date,
            Status = RecurringBillingStatus.Retrying, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();
        return (db, sub.Id);
    }

    private static async Task<SubscriptionBillingState> EstadoAsync(AppDbContext db) =>
        await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();

    [Fact]
    public async Task La_cancelacion_inmediata_para_el_estado_de_cobro()
    {
        var (db, _) = await SembrarAsync();
        await using var _db = db;

        var r = await new CancelMembershipHandler(db, Reloj(), Mock.Of<ISponsorBonusService>())
            .Handle(new CancelMembershipCommand(Miembro, "prueba"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        var estado = await EstadoAsync(db);
        estado.Status.Should().Be(RecurringBillingStatus.Stopped);
        estado.LastFailureReason.Should().Contain("MEMBERSHIP_CANCELLED");
    }

    [Fact]
    public async Task La_cancelacion_programada_no_para_el_estado_hasta_su_fecha()
    {
        var (db, _) = await SembrarAsync();
        await using var _db = db;

        await new CancelMembershipHandler(db, Reloj(), Mock.Of<ISponsorBonusService>())
            .Handle(new CancelMembershipCommand(Miembro, "prueba", Ahora.AddDays(10)), CancellationToken.None);

        (await EstadoAsync(db)).Status.Should().Be(RecurringBillingStatus.Retrying);
    }

    [Fact]
    public async Task El_trabajo_de_cancelaciones_programadas_para_el_estado_al_cerrarlas()
    {
        var (db, _) = await SembrarAsync(cancelacion: Ahora.AddDays(-1));
        await using var _db = db;

        await new ProcessScheduledCancellationsJob(db, Reloj(), Mock.Of<ISponsorBonusService>()).ExecuteAsync();

        var estado = await EstadoAsync(db);
        estado.Status.Should().Be(RecurringBillingStatus.Stopped);
        estado.LastFailureReason.Should().Contain("MEMBERSHIP_CANCELLED");
    }

    [Fact]
    public async Task Los_estados_de_otras_suscripciones_no_se_tocan()
    {
        var (db, _) = await SembrarAsync();
        await using var _db = db;
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "otro", MembershipSubscriptionId = "otra-sub", MemberId = "AMB-OTRO", RecurringBillingPlanId = 1,
            BillingAnchorDate = Ahora, NextBillingDate = Ahora, NextAttemptDate = Ahora,
            Status = RecurringBillingStatus.Active, CreatedBy = "seed"
        });
        await db.SaveChangesAsync();

        await new CancelMembershipHandler(db, Reloj(), Mock.Of<ISponsorBonusService>())
            .Handle(new CancelMembershipCommand(Miembro, "prueba"), CancellationToken.None);

        (await db.SubscriptionBillingStates.AsNoTracking().SingleAsync(s => s.Id == "otro"))
            .Status.Should().Be(RecurringBillingStatus.Active);
    }

    /// <summary>
    /// Revision de Q3 (M1): una retenida por el admin (OnHold con la marca) cuya cancelacion programada llega se cancela sin
    /// la marca, y su cobro parado por retencion queda sellado como cancelado: reactivar la cuenta ya no la reanuda.
    /// </summary>
    [Fact]
    public async Task El_trabajo_de_cancelaciones_programadas_quita_la_marca_de_la_retenida_por_el_admin()
    {
        var (db, subId) = await SembrarAsync(cancelacion: Ahora.AddDays(-1));
        await using var _db = db;
        var sub = await db.MembershipSubscriptions.SingleAsync(s => s.Id == subId);
        sub.SubscriptionStatus  = MembershipStatus.OnHold;
        sub.HeldByAccountStatus = MemberAccountStatus.Suspended;
        var estado = await db.SubscriptionBillingStates.SingleAsync();
        estado.Status            = RecurringBillingStatus.Stopped;
        estado.LastFailureReason = "MEMBERSHIP_ON_HOLD";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await new ProcessScheduledCancellationsJob(db, Reloj(), Mock.Of<ISponsorBonusService>()).ExecuteAsync();

        var cancelada = await db.MembershipSubscriptions.AsNoTracking().SingleAsync(s => s.Id == subId);
        cancelada.SubscriptionStatus.Should().Be(MembershipStatus.Cancelled);
        cancelada.HeldByAccountStatus.Should().BeNull();
        (await EstadoAsync(db)).LastFailureReason.Should().Be("MEMBERSHIP_CANCELLED");
    }
}
