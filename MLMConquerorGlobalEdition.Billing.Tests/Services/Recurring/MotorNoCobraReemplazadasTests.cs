using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MLMConquerorGlobalEdition.Billing.Services;
using MLMConquerorGlobalEdition.Billing.Services.Recurring;
using MLMConquerorGlobalEdition.Billing.Services.Routing;
using MLMConquerorGlobalEdition.Billing.Tests.Helpers;
using MLMConquerorGlobalEdition.Domain.Entities.Billing;
using MLMConquerorGlobalEdition.Domain.Entities.Commission;
using MLMConquerorGlobalEdition.Domain.Entities.Membership;
using MLMConquerorGlobalEdition.Domain.Enums;
using MLMConquerorGlobalEdition.Repository.Context;
using MLMConquerorGlobalEdition.SharedKernel;

namespace MLMConquerorGlobalEdition.Billing.Tests.Services.Recurring;

/// <summary>
/// Plan Q (tercera revision del bloque 2, C-a, defensa en profundidad): una suscripcion caducada porque otra la reemplazo
/// (subida o bajada) no se cobra ni se reactiva, aunque su estado de cobro siga vivo (filas de antes del arreglo) y aunque
/// se fuerce. Antes el motor cobraba el nivel viejo y la reactivaba: dos activas, los puntos dos veces y dos cobros.
/// </summary>
public class MotorNoCobraReemplazadasTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 0, 0);
    private const string Miembro = "AMB-000001";

    private static async Task<AppDbContext> SembrarAsync(bool conNueva = true)
    {
        var db = TestDbContextFactory.Create();
        db.RecurringBillingPlans.Add(new RecurringBillingPlan
        {
            Id = 1, Name = "Travel Advantage", CycleType = RecurringCycleType.Every30Days, RetryCadenceDays = "1,2",
            OnAllRetriesFail = RecurringFailurePolicy.RetryOnMonthlyAnniversary, StopAfterUnbilledDays = 90,
            PayFromCommissionBalanceFirst = true, TokenTypeId = 100, IsActive = true, CreatedBy = "prueba"
        });
        db.MembershipLevels.AddRange(
            new MembershipLevel { Id = 2, Name = "Travel Advantage - VIP", SortOrder = 2, CreatedBy = "prueba" },
            new MembershipLevel { Id = 3, Name = "Travel Advantage - Elite", SortOrder = 3, CreatedBy = "prueba" });
        db.Products.Add(new MLMConquerorGlobalEdition.Domain.Entities.Orders.Product
        {
            Id = "prod-vip", Name = "Travel Advantage VIP", Description = "", ImageUrl = "", MembershipLevelId = 2,
            MonthlyFee = 59.97m, SetupFee = 0m, IsActive = true, CreatedBy = "prueba"
        });
        db.MembershipSubscriptions.Add(new MembershipSubscription
        {
            Id = "sub-vieja", MemberId = Miembro, MembershipLevelId = 2, SubscriptionStatus = MembershipStatus.Expired,
            StartDate = Ahora.AddDays(-60), EndDate = Ahora.AddDays(-10), IsAutoRenew = true,
            CreatedBy = "prueba", CreationDate = Ahora.AddDays(-60)
        });
        if (conNueva)
            db.MembershipSubscriptions.Add(new MembershipSubscription
            {
                Id = "sub-nueva", MemberId = Miembro, MembershipLevelId = 3, PreviousMembershipLevelId = 2,
                ChangeReason = SubscriptionChangeReason.Upgrade, SubscriptionStatus = MembershipStatus.Active,
                StartDate = Ahora.AddDays(-10), IsAutoRenew = true, CreatedBy = "prueba", CreationDate = Ahora.AddDays(-10)
            });
        // El estado de la vieja sigue vivo (una subida de antes del arreglo no lo paraba).
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado-viejo", MembershipSubscriptionId = "sub-vieja", MemberId = Miembro, RecurringBillingPlanId = 1,
            BillingAnchorDate = Ahora.AddDays(-60), NextBillingDate = Ahora.Date, NextAttemptDate = Ahora.Date,
            Status = RecurringBillingStatus.Active, CreatedBy = "prueba"
        });
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = Miembro, CommissionTypeId = 1, Amount = 500m, Status = CommissionEarningStatus.Pending,
            EarnedDate = Ahora.AddDays(-8), PaymentDate = Ahora.AddDays(-1), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();
        return db;
    }

    private static RecurringBillingProcessor Procesador(AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);
        return new RecurringBillingProcessor(
            db, new CommissionBalanceService(db, reloj.Object), new RecurringBillingScheduler(),
            new Mock<IGatewayRouter>().Object, new Mock<IGatewayChargeOrchestrator>().Object,
            new Mock<ICardBrandDetector>().Object, new TransactionOnlyGate(db),
            new Mock<MLMConquerorGlobalEdition.Billing.Services.AnnualFee.IAnnualFeeChargeService>().Object, reloj.Object,
            NullLogger<RecurringBillingProcessor>.Instance);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task No_cobra_ni_reactiva_la_vieja_reemplazada(bool forzado)
    {
        await using var db = await SembrarAsync();

        var r = await Procesador(db).ProcessAsync("estado-viejo", forceBillNow: forzado);

        r.Value!.Outcome.Should().Be("Skipped");
        r.Value.FailureReason.Should().Be(RecurringBillingProcessor.ReasonMembershipReplaced);
        (await db.Orders.CountAsync()).Should().Be(0);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.Status.Should().Be(RecurringBillingStatus.Stopped);
        estado.LastFailureReason.Should().Be(RecurringBillingProcessor.ReasonMembershipReplaced);
        var subs = await db.MembershipSubscriptions.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.SubscriptionStatus);
        subs["sub-vieja"].Should().Be(MembershipStatus.Expired);
        subs["sub-nueva"].Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task Una_caducada_sin_reemplazo_se_sigue_cobrando_y_reactivando()
    {
        // Caducada por el motor (MarkExpired) sin otra suscripcion: el cobro la reactiva, como antes.
        await using var db = await SembrarAsync(conNueva: false);

        var r = await Procesador(db).ProcessAsync("estado-viejo");

        r.Value!.Outcome.Should().Be("Success");
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync()).SubscriptionStatus.Should().Be(MembershipStatus.Active);
    }
}
