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
/// Plan Q (revision 2 del bloque 2, I5): un cobro que sale bien reactiva la suscripcion DEL ESTADO que se cobro. Antes
/// buscaba cualquier suscripcion del miembro retenida o caducada sin orden: tras una subida podia reactivar la vieja
/// (caducada), dejar retenida la que se acababa de cobrar y sumar dos veces los puntos de la linea (DQ16).
/// </summary>
public class ReactivaLaSuscripcionDelEstadoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 0, 0);
    private const string Miembro = "AMB-000001";

    [Fact]
    public async Task Reactiva_la_suscripcion_cobrada_y_no_la_vieja_caducada_de_una_subida()
    {
        await using var db = TestDbContextFactory.Create();
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
            Id = "prod-elite", Name = "Travel Advantage Elite", Description = "", ImageUrl = "", MembershipLevelId = 3,
            MonthlyFee = 119.97m, SetupFee = 0m, IsActive = true, CreatedBy = "prueba"
        });
        // La vieja (caducada por la subida) va primero: un FirstOrDefault sin filtro por el estado la elegia a ella.
        db.MembershipSubscriptions.AddRange(
            new MembershipSubscription
            {
                Id = "sub-vieja", MemberId = Miembro, MembershipLevelId = 2, SubscriptionStatus = MembershipStatus.Expired,
                StartDate = Ahora.AddDays(-90), CreatedBy = "prueba"
            },
            new MembershipSubscription
            {
                Id = "sub-1", MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.HoldByBilling,
                PreviousMembershipLevelId = 2, StartDate = Ahora.AddDays(-30), IsAutoRenew = true, CreatedBy = "prueba"
            });
        db.SubscriptionBillingStates.Add(new SubscriptionBillingState
        {
            Id = "estado-1", MembershipSubscriptionId = "sub-1", MemberId = Miembro, RecurringBillingPlanId = 1,
            BillingAnchorDate = Ahora.AddDays(-30), NextBillingDate = Ahora.Date, NextAttemptDate = Ahora.Date,
            Status = RecurringBillingStatus.Active, CreatedBy = "prueba"
        });
        db.CommissionEarnings.Add(new CommissionEarning
        {
            BeneficiaryMemberId = Miembro, CommissionTypeId = 1, Amount = 500m, Status = CommissionEarningStatus.Pending,
            EarnedDate = Ahora.AddDays(-8), PaymentDate = Ahora.AddDays(-1), CreatedBy = "prueba"
        });
        await db.SaveChangesAsync();

        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);
        var procesador = new RecurringBillingProcessor(
            db, new CommissionBalanceService(db, reloj.Object), new RecurringBillingScheduler(),
            new Mock<IGatewayRouter>().Object, new Mock<IGatewayChargeOrchestrator>().Object,
            new Mock<ICardBrandDetector>().Object, new TransactionOnlyGate(db),
            new Mock<MLMConquerorGlobalEdition.Billing.Services.AnnualFee.IAnnualFeeChargeService>().Object, reloj.Object,
            NullLogger<RecurringBillingProcessor>.Instance);

        var r = await procesador.ProcessAsync("estado-1");

        r.Value!.Outcome.Should().Be("Success");
        var subs = await db.MembershipSubscriptions.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.SubscriptionStatus);
        subs["sub-1"].Should().Be(MembershipStatus.Active, "es la que se cobro");
        subs["sub-vieja"].Should().Be(MembershipStatus.Expired, "la de antes de la subida sigue caducada");
    }
}
