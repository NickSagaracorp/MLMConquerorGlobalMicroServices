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
/// Plan K (revisión, C2): el motor no cobra una membresía cancelada, una con la cancelación programada
/// ya cumplida ni una gratuita. Para el estado sin cobrar, sin pedido y sin tocar la membresía. Antes
/// cobraba comisiones a quien había cancelado (el estado seguía vivo).
/// </summary>
public class MotorNoCobraCanceladasTests
{
    private static readonly DateTime Ahora = new(2026, 10, 20, 9, 0, 0);
    private const string Miembro = "AMB-000001";

    private static async Task<AppDbContext> SembrarAsync(Action<MembershipSubscription> ajustar)
    {
        var db = TestDbContextFactory.Create();
        db.RecurringBillingPlans.Add(new RecurringBillingPlan
        {
            Id = 1, Name = "Travel Advantage", CycleType = RecurringCycleType.Every30Days, RetryCadenceDays = "1,2",
            OnAllRetriesFail = RecurringFailurePolicy.RetryOnMonthlyAnniversary, StopAfterUnbilledDays = 90,
            PayFromCommissionBalanceFirst = true, TokenTypeId = 100, IsActive = true, CreatedBy = "prueba"
        });
        db.MembershipLevels.Add(new MembershipLevel { Id = 3, Name = "Travel Advantage - Elite", SortOrder = 3, CreatedBy = "prueba" });
        db.Products.Add(new MLMConquerorGlobalEdition.Domain.Entities.Orders.Product
        {
            Id = "prod-elite", Name = "Travel Advantage Elite", Description = "", ImageUrl = "", MembershipLevelId = 3,
            MonthlyFee = 119.97m, SetupFee = 0m, IsActive = true, CreatedBy = "prueba"
        });
        var sub = new MembershipSubscription
        {
            Id = "sub-1", MemberId = Miembro, MembershipLevelId = 3, SubscriptionStatus = MembershipStatus.Active,
            StartDate = Ahora.AddDays(-30), IsAutoRenew = true, CreatedBy = "prueba"
        };
        ajustar(sub);
        db.MembershipSubscriptions.Add(sub);
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
        return db;
    }

    private static RecurringBillingProcessor Procesador(AppDbContext db)
    {
        var reloj = new Mock<IDateTimeProvider>();
        reloj.Setup(r => r.Now).Returns(Ahora);
        return new RecurringBillingProcessor(
            db, new CommissionBalanceService(db, reloj.Object), new RecurringBillingScheduler(),
            new Mock<IGatewayRouter>().Object, new Mock<IGatewayChargeOrchestrator>().Object,
            new Mock<ICardBrandDetector>().Object, new TransactionOnlyGate(db), new Mock<MLMConquerorGlobalEdition.Billing.Services.AnnualFee.IAnnualFeeChargeService>().Object, reloj.Object,
            NullLogger<RecurringBillingProcessor>.Instance);
    }

    public static TheoryData<string, Action<MembershipSubscription>, string> Casos => new()
    {
        { "cancelada", s => { s.SubscriptionStatus = MembershipStatus.Cancelled; s.IsAutoRenew = false; }, RecurringBillingProcessor.ReasonMembershipCancelled },
        { "cancelacion cumplida hoy", s => { s.IsAutoRenew = false; s.CancellationDate = new DateTime(2026, 10, 20); }, RecurringBillingProcessor.ReasonMembershipCancelled },
        { "cancelacion cumplida ayer", s => { s.IsAutoRenew = false; s.CancellationDate = new DateTime(2026, 10, 19, 23, 0, 0); }, RecurringBillingProcessor.ReasonMembershipCancelled },
        { "gratuita", s => s.IsFree = true, RecurringBillingProcessor.ReasonMembershipFree },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task No_cobra_y_para_el_estado(string caso, Action<MembershipSubscription> ajustar, string razon)
    {
        await using var db = await SembrarAsync(ajustar);

        var r = await Procesador(db).ProcessAsync("estado-1");

        r.IsSuccess.Should().BeTrue(caso);
        r.Value!.Outcome.Should().Be("Skipped");
        r.Value.FailureReason.Should().Be(razon);
        (await db.Orders.CountAsync()).Should().Be(0);
        (await db.CommissionEarnings.CountAsync(e => e.Amount < 0)).Should().Be(0);
        var estado = await db.SubscriptionBillingStates.AsNoTracking().SingleAsync();
        estado.Status.Should().Be(RecurringBillingStatus.Stopped);
        estado.LastFailureReason.Should().Be(razon);
        // La membresía no se toca: ni HoldByBilling ni Expired encima de una cancelación.
        (await db.MembershipSubscriptions.AsNoTracking().SingleAsync()).SubscriptionStatus
            .Should().NotBe(MembershipStatus.HoldByBilling).And.NotBe(MembershipStatus.Expired);
    }

    [Fact]
    public async Task Forzado_tampoco_cobra_una_cancelada()
    {
        await using var db = await SembrarAsync(s => { s.SubscriptionStatus = MembershipStatus.Cancelled; });

        var r = await Procesador(db).ProcessAsync("estado-1", forceBillNow: true);

        r.Value!.Outcome.Should().Be("Skipped");
        (await db.Orders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Con_la_cancelacion_programada_aun_por_llegar_sigue_cobrando()
    {
        await using var db = await SembrarAsync(s => { s.IsAutoRenew = false; s.CancellationDate = Ahora.AddDays(5); });

        var r = await Procesador(db).ProcessAsync("estado-1");

        r.Value!.Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task Sin_renovacion_automatica_pero_sin_fecha_de_cancelacion_sigue_cobrando()
    {
        // IsAutoRenew vale false por defecto en muchas filas: solo con la fecha de cancelación cumplida para.
        await using var db = await SembrarAsync(s => s.IsAutoRenew = false);

        var r = await Procesador(db).ProcessAsync("estado-1");

        r.Value!.Outcome.Should().Be("Success");
    }
}
